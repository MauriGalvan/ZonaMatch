"""
Scrapes Zonaprop listing pages to measure the real estate market of each ZonaMatch zone.

For every zone in datos/zonaprop/zones.csv (exported by target_zones.sql), every operation (venta, alquiler) and every
property type (departamentos, casas, ph) it reads the newest listings, stratified by number of rooms:
  - page 1 of the type, which gives the total number of active listings. When it has 30 listings or fewer that page
    is the whole market of the zone and nothing else is needed;
  - otherwise one page (30 newest listings) for each room group: 1, 2, 3, 4 and 5 or more rooms. Each page also gives
    the total of its group, so the averages can be weighted by the real mix of the zone (calculate_market.sql) and
    every group gets prices of its own.
It also reads the totals of four Zonaprop filters, for the three types together: sale listings eligible for a mortgage
("apto crédito"), sale and rent listings published by the owner ("dueño directo") and rentals that accept pets.

Zonaprop resolves its location URLs by slug, which usually matches ours ("villa-crespo", "adrogue"). An unknown
slug falls back to a wider search and an ambiguous one may land in another province, so every response is
checked against the expected location (its name, in CABA or in an AMBA partido) before it is used. Names repeated
elsewhere in the country are tried again with the partido ("castelar-moron"). Zones that still do not match are
logged in datos/zonaprop/unmatched.csv; slug_overrides.csv maps them by hand (a Zonaprop slug, or the partido of
zones that geo.zonas does not place in one).

Every page is cached in datos/zonaprop/cache/<run>/, one folder per scrape (the date it started), with every field of
each listing that the market calculation may need. Running it again resumes the same run while it is younger than
--max-age-days; after that a new run starts from scratch.

Outputs (in datos/zonaprop/):
  totals.csv    zone_slug, operation, property_type, rooms, filter, listings, zonaprop_location, zonaprop_slug, url,
                scraped_at. property_type 'all' and a filter (credit, owner, pets) for the filter totals; rooms 0 for
                every room count, 1 to 5 (5 or more) for a room group
  listings.csv  one row per sampled listing, with the room group (stratum) it was sampled from: 0 when page 1 had the
                whole market
Statistics per zone are computed in SQL (etl/zonaprop/calculate_market.sql), which also drops listings whose
coordinates fall far from the zone.

The scraper is deliberately slow: one request every few seconds, from one connection, and it never tries to get past
Cloudflare. When Zonaprop answers with a challenge or an error it waits longer and longer, and finally stops; running
it again resumes where it stopped.

Usage (from backend/ZonaMatch):
  python etl/zonaprop/scrape_zonaprop.py                 scrape everything still missing
  python etl/zonaprop/scrape_zonaprop.py --only palermo,adrogue
  python etl/zonaprop/scrape_zonaprop.py --export-only   rebuild the CSV files from the cache
No dependencies beyond the Python standard library.
"""

import argparse
import csv
import gzip
import json
import random
import re
import sys
import time
import unicodedata
import urllib.error
import urllib.request
from datetime import date, datetime, timedelta, timezone
from pathlib import Path

BASE_URL = "https://www.zonaprop.com.ar"
DATA_DIR = Path(__file__).resolve().parents[2] / "datos" / "zonaprop"
OVERRIDES_FILE = Path(__file__).resolve().parent / "slug_overrides.csv"

OPERATIONS = ["venta", "alquiler"]
# Zonaprop URL segment -> property type name in the listing data
PROPERTY_TYPES = {"departamentos": "Departamentos", "casas": "Casas", "ph": "PH"}
ALL_TYPES = "departamentos-casas-ph"
# Room groups and their URL segment. "mas-de-5-ambientes" is 5 or more
ROOM_GROUPS = {1: "1-ambiente", 2: "2-ambientes", 3: "3-ambientes", 4: "4-ambientes", 5: "mas-de-5-ambientes"}
# Filter totals per operation, for the three types together: key -> URL segment
FILTERS = {
    "venta": {"credit": "con-apto-credito", "owner": "dueno-directo"},
    "alquiler": {"pets": "con-permite-mascotas", "owner": "dueno-directo"},
}
PAGE_SIZE = 30
# Newest first: the default "relevance" order favors paid listings
SORT_SUFFIX = "-orden-publicado-descendente"
USD_RATE_URL = "https://dolarapi.com/v1/dolares/bolsa"
# publisher.publisherTypeId of Zonaprop
PUBLISHER_TYPES = {"1": "owner", "2": "agency"}

USER_AGENT = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) "
    "Chrome/130.0 Safari/537.36"
)
STATE_MARKER = "window.__PRELOADED_STATE__ = "
TITLE_PATTERN = re.compile(r"<title>\s*([^<]*?)\s*</title>", re.S)
# "1.234 Departamentos más recientes en venta en Adrogué, Almirante Brown - Pagina 2 - Zonaprop"
LOCATION_PATTERN = re.compile(r" en (?:venta|alquiler)(?: [^,]*?)? en (.+?)(?: - P[aá]gina \d+)? - Zonaprop$")


# Parents Zonaprop shows for AMBA locations outside CABA: the partidos and its GBA regions
AMBA_PARENTS = {
    "almirante brown", "avellaneda", "berazategui", "berisso", "canuelas", "ensenada", "escobar",
    "esteban echeverria", "exaltacion de la cruz", "ezeiza", "florencio varela", "general rodriguez",
    "general san martin", "san martin", "hurlingham", "ituzaingo", "jose c paz", "la matanza", "la plata", "lanus",
    "lomas de zamora", "lujan", "malvinas argentinas", "marcos paz", "merlo", "moreno", "moron", "pilar",
    "presidente peron", "quilmes", "san fernando", "san isidro", "san miguel", "san vicente", "tigre",
    "tres de febrero", "vicente lopez", "general las heras", "gba norte", "gba sur", "gba oeste",
}
# Zonaprop also nests some barrios in a localidad ("Villa La Florida, Bernal Oeste"): read_zones adds our zone names
KNOWN_PARENTS = set(AMBA_PARENTS)


class Blocked(Exception):
    """Zonaprop answered with a challenge or an error page instead of the listing."""


class NotFound(Exception):
    """Zonaprop has no listing page for that slug (HTTP 404 or 410)."""


def normalize(text):
    """Lowercase ASCII without accents or punctuation, to compare location names."""
    text = unicodedata.normalize("NFKD", text or "").encode("ascii", "ignore").decode()
    text = re.sub(r"^partido de ", "", text.lower().strip())
    return re.sub(r"[^a-z0-9]+", " ", text).strip()


def fetch(path):
    """Downloads a listing page and returns (final_url, html). Raises Blocked on challenges."""
    request = urllib.request.Request(
        BASE_URL + path,
        headers={
            "User-Agent": USER_AGENT,
            "Accept": "text/html,application/xhtml+xml",
            "Accept-Language": "es-AR,es;q=0.9",
            "Accept-Encoding": "gzip",
        },
    )
    try:
        with urllib.request.urlopen(request, timeout=60) as response:
            body = response.read()
            if response.headers.get("Content-Encoding") == "gzip":
                body = gzip.decompress(body)
            html = body.decode("utf-8", errors="replace")
            final_url = response.geturl()
    except urllib.error.HTTPError as error:
        if error.code in (404, 410):
            raise NotFound() from error
        raise Blocked(f"HTTP {error.code}") from error
    except (urllib.error.URLError, TimeoutError, ConnectionError) as error:
        raise Blocked(str(error)) from error
    if STATE_MARKER not in html:
        raise Blocked("page without listing data (challenge?)")
    return final_url, html


def parse_page(html):
    """Extracts the title, the total and the fields of each listing from a Zonaprop page."""
    title_match = TITLE_PATTERN.search(html)
    title = title_match.group(1) if title_match else ""
    start = html.index(STATE_MARKER) + len(STATE_MARKER)
    state, _ = json.JSONDecoder().raw_decode(html, start)
    store = state["listStore"]
    paging = store.get("paging") or {}
    total = int(paging.get("total") or 0)
    postings = [parse_posting(posting) for posting in (store.get("listPostings") or [] if total > 0 else [])]
    return {"title": title, "total": total, "total_pages": paging.get("totalPages") or 0, "postings": postings}


def number(value):
    try:
        return float(str(value).replace(",", "."))
    except (TypeError, ValueError):
        return None


def feature(features, key):
    return number((features.get(key) or {}).get("value"))


def location_chain(location):
    """'Palermo Hollywood > Palermo > Capital Federal' as a list of names, innermost first."""
    names = []
    node = location.get("location") or {}
    while node and len(names) < 6:
        names.append(node.get("name"))
        node = node.get("parent") or {}
    return names


def parse_posting(posting):
    features = posting.get("mainFeatures") or {}
    location = posting.get("postingLocation") or {}
    geo = (location.get("postingGeolocation") or {}).get("geolocation") or {}
    expenses = posting.get("expenses") or {}
    publisher = posting.get("publisher") or {}
    pictures = (posting.get("visiblePictures") or {}).get("pictures") or []
    operations = {}
    for operation in posting.get("priceOperationTypes") or []:
        prices = operation.get("prices") or []
        if prices:
            operations[operation["operationType"]["name"].lower()] = {
                "currency": prices[0].get("currency"),
                "amount": prices[0].get("amount"),
            }
    return {
        "id": posting.get("postingId"),
        "type": (posting.get("realEstateType") or {}).get("name"),
        "operations": operations,
        "expenses": expenses.get("amount"),
        "expenses_currency": expenses.get("currency"),
        "total_m2": feature(features, "CFT100"),
        "covered_m2": feature(features, "CFT101"),
        "rooms": feature(features, "CFT1"),
        "bedrooms": feature(features, "CFT2"),
        "bathrooms": feature(features, "CFT3"),
        "garages": feature(features, "CFT7"),
        "age_years": feature(features, "CFT5"),
        "publisher_type": PUBLISHER_TYPES.get(str(publisher.get("publisherTypeId")), None),
        "publisher_id": publisher.get("publisherId"),
        "publisher_name": publisher.get("name"),
        "lat": geo.get("latitude"),
        "lng": geo.get("longitude"),
        "location_names": location_chain(location),
        "address": (location.get("address") or {}).get("name"),
        "title": posting.get("title"),
        "pill": posting.get("triggerPill"),
        "picture": (pictures[0].get("url360x266") if pictures else None),
        "status": posting.get("status"),
        "modified": posting.get("modified_date"),
        "url": posting.get("url"),
    }


def location_matches(title, zone):
    """True when the page title names the zone we asked for, in CABA or in an AMBA partido.

    The partido is not compared with ours: Zonaprop places some localidades in the neighboring partido (Loma
    Hermosa, Villa Adelina) and answers with the whole partido for the cabeceras ("Morón, GBA Oeste").
    calculate_market.sql checks the coordinates of the listings, which catches a same-named place elsewhere in the
    AMBA (Villanueva in Tigre instead of Moreno) and scales a partido total down to the zone.
    """
    match = LOCATION_PATTERN.search(title)
    if not match:
        return False, ""
    location = match.group(1)
    name, _, parent = location.partition(", ")
    parent = normalize(parent)
    if zone["manual_slug"]:
        # Mapped by hand (slug_overrides.csv): the name may differ ("Villa Ballester" for Villa Ballester Noreste)
        return parent in KNOWN_PARENTS or parent in ("caba", "capital federal"), location
    if normalize(name) != normalize(zone["name"]):
        return False, location
    if zone["in_caba"]:
        return parent == "caba", location
    return parent in KNOWN_PARENTS, location


def listing_path(property_type, operation, slug, segment="", page=1, sort=True):
    """'/departamentos-venta-palermo-2-ambientes-orden-publicado-descendente-pagina-2.html'"""
    parts = [property_type, operation, slug] + ([segment] if segment else [])
    page_part = f"-pagina-{page}" if page > 1 else ""
    return f"/{'-'.join(parts)}{SORT_SUFFIX if sort else ''}{page_part}.html"


def run_folder(cache_root, requested, max_age):
    """The cache folder of this scrape: the newest run still younger than max_age, or a new one named after today."""
    if requested:
        return cache_root / requested
    runs = sorted(p for p in cache_root.glob("????-??-??") if p.is_dir())
    if runs:
        started = date.fromisoformat(runs[-1].name)
        if date.today() - started < max_age:
            return runs[-1]
    return cache_root / date.today().isoformat()


class Scraper:
    def __init__(self, cache_dir, delay, download=True):
        self.cache_dir = cache_dir
        self.delay = delay
        self.download = download
        self.requests = 0
        self.last_request = 0.0
        if download:
            cache_dir.mkdir(parents=True, exist_ok=True)

    def cache_file(self, path):
        return self.cache_dir / (path.strip("/").replace(".html", "") + ".json")

    def get(self, path):
        """Returns the parsed page, from the cache when this run already has it. None when not downloading."""
        file = self.cache_file(path)
        if file.exists():
            return json.loads(file.read_text(encoding="utf-8"))
        if not self.download:
            return None
        # Longer and longer pauses when Zonaprop refuses; after the last one the scrape stops (and can be resumed)
        waits = [60, 180, 600, 1800, 1800]
        for attempt in range(len(waits) + 1):
            # One request every `delay` seconds on average, counting the time the previous one took
            pause = self.delay * random.uniform(0.7, 1.4) - (time.time() - self.last_request)
            if pause > 0:
                time.sleep(pause)
            self.last_request = time.time()
            try:
                final_url, html = fetch(path)
                break
            except NotFound:
                # Cached too, so a slug Zonaprop does not know is asked only once per run
                self.requests += 1
                page = {"title": "", "total": 0, "total_pages": 0, "postings": [], "final_url": None,
                        "scraped_at": datetime.now(timezone.utc).isoformat(timespec="seconds")}
                file.write_text(json.dumps(page), encoding="utf-8")
                return page
            except Blocked as error:
                if attempt == len(waits):
                    raise
                print(f"    Zonaprop no respondio ({error}); esperando {waits[attempt] // 60} min...", flush=True)
                time.sleep(waits[attempt])
        self.requests += 1
        page = parse_page(html)
        page["final_url"] = final_url
        page["scraped_at"] = datetime.now(timezone.utc).isoformat(timespec="seconds")
        file.write_text(json.dumps(page, ensure_ascii=False), encoding="utf-8")
        return page

    def resolve(self, zone):
        """Finds the Zonaprop slug of a zone, trying each candidate. Returns (slug, location) or (None, answer)."""
        answer = ""
        for slug in zone["candidates"]:
            page = self.get(listing_path("departamentos", "venta", slug))
            if page is None:
                continue
            matches, location = location_matches(page["title"], zone)
            if matches:
                return slug, location
            answer = answer or location or page["title"]
        return None, answer

    def pages(self, slug):
        """Yields (operation, property_type, room_group, page) for every page of a matched zone, downloading them.

        room_group is 0 for page 1 of the type, 1-5 for the page of a room group (only when page 1 is not the whole
        market). A page missing from the cache when not downloading ends that branch.
        """
        for operation in OPERATIONS:
            for property_type in PROPERTY_TYPES:
                first = self.get(listing_path(property_type, operation, slug))
                if first is None:
                    continue
                yield operation, property_type, 0, first
                if first["total"] <= PAGE_SIZE:
                    continue
                for rooms, segment in ROOM_GROUPS.items():
                    page = self.get(listing_path(property_type, operation, slug, segment))
                    if page is not None:
                        yield operation, property_type, rooms, page

    def filter_pages(self, slug, operation_totals):
        """Yields (operation, filter, page) for the filter totals of the operations that have listings."""
        for operation, filters in FILTERS.items():
            if not operation_totals.get(operation):
                continue
            for key, segment in filters.items():
                page = self.get(listing_path(ALL_TYPES, operation, slug, segment, sort=False))
                if page is not None:
                    yield operation, key, page

    def scrape_zone(self, zone):
        """Downloads every page of a zone. Returns (False, answer) when Zonaprop does not know it."""
        slug, location = self.resolve(zone)
        if not slug:
            return False, location
        totals = {}
        for operation, _, rooms, page in self.pages(slug):
            if rooms == 0:
                totals[operation] = totals.get(operation, 0) + page["total"]
        for _ in self.filter_pages(slug, totals):
            pass
        return True, location


def slugify(text):
    """'Partido de Morón' -> 'moron', the way Zonaprop builds its location slugs."""
    return normalize(text).replace(" ", "-")


def read_zones(only):
    file = DATA_DIR / "zones.csv"
    if not file.exists():
        sys.exit(f"Falta {file}: exportarlo con etl/zonaprop/target_zones.sql (ver MERCADO-ZONAS-DOCKER.bat).")
    # zonaprop_slug: the slug to use; partido: for zones without a partido in geo.zonas, to build the fallback
    overrides = {}
    if OVERRIDES_FILE.exists():
        with OVERRIDES_FILE.open(encoding="utf-8") as handle:
            overrides = {row["zone_slug"]: row for row in csv.DictReader(handle)}
    zones = []
    with file.open(encoding="utf-8") as handle:
        for row in csv.DictReader(handle):
            if row["in_caba"] != "t":
                KNOWN_PARENTS.add(normalize(row["name"]))
            if only and row["slug"] not in only:
                continue
            override = overrides.get(row["slug"], {})
            row["in_caba"] = row["in_caba"] == "t"
            row["parent_name"] = row["parent_name"] or override.get("partido") or ""
            row["manual_slug"] = override.get("zonaprop_slug") or ""
            if row["manual_slug"] == "-":  # explicitly excluded
                continue
            if row["manual_slug"]:
                row["candidates"] = [row["manual_slug"]]
            else:
                # Names repeated elsewhere in the country carry the partido in Zonaprop ("castelar-moron")
                row["candidates"] = [row["slug"]]
                if row["parent_name"] and not row["in_caba"]:
                    row["candidates"].append(f"{row['slug']}-{slugify(row['parent_name'])}")
            zones.append(row)
    return zones


def usd_rate(override):
    """Pesos per dollar (MEP, from dolarapi.com) to convert USD rents and peso sale prices."""
    if override:
        return override
    try:
        request = urllib.request.Request(USD_RATE_URL, headers={"User-Agent": USER_AGENT})
        with urllib.request.urlopen(request, timeout=30) as response:
            return float(json.loads(response.read())["venta"])
    except (urllib.error.URLError, TimeoutError, KeyError, ValueError) as error:
        sys.exit(f"No se pudo obtener la cotizacion del dolar ({error}); pasarla con --usd-ars.")


def room_group(rooms):
    """Room group of a listing from its own number of rooms (1-5, 5 = 5 or more), None when unknown."""
    if rooms is None or rooms < 1:
        return None
    return min(int(rooms), 5)


TOTALS_HEADER = ["zone_slug", "operation", "property_type", "rooms", "filter", "listings", "zonaprop_location",
                 "zonaprop_slug", "url", "scraped_at"]
LISTINGS_HEADER = ["zone_slug", "operation", "property_type", "stratum", "posting_id", "currency", "price",
                   "expenses", "expenses_currency", "total_m2", "covered_m2", "rooms", "bedrooms", "bathrooms",
                   "garages", "age_years", "publisher_type", "lat", "lng", "modified", "url", "usd_ars"]


def zone_rows(scraper, zone, usd_ars):
    """totals.csv and listings.csv rows of a zone from the cache, or (None, answer) when it did not match."""
    slug, location = scraper.resolve(zone)
    if not slug:
        return None, location
    totals, listings = [], []
    operation_totals = {}
    seen = set()
    first_pages = {}
    for operation, property_type, rooms, page in scraper.pages(slug):
        type_name = PROPERTY_TYPES[property_type]
        segment = ROOM_GROUPS.get(rooms, "")
        url = BASE_URL + listing_path(property_type, operation, slug, segment)
        totals.append([zone["slug"], operation, property_type, rooms, "", page["total"], location, slug, url,
                       page["scraped_at"]])
        if rooms == 0:
            operation_totals[operation] = operation_totals.get(operation, 0) + page["total"]
            first_pages[(operation, property_type)] = page
            # Page 1 is a sample of its own only when it is the whole market; otherwise the room pages are the sample
            if page["total"] > PAGE_SIZE:
                continue
        for posting in page["postings"]:
            price = posting["operations"].get(operation)
            key = (operation, property_type, posting["id"])
            if not price or posting["type"] != type_name or key in seen:
                continue
            seen.add(key)
            listings.append([
                zone["slug"], operation, property_type, rooms, posting["id"], price["currency"], price["amount"],
                posting.get("expenses"), posting.get("expenses_currency"), posting["total_m2"],
                posting["covered_m2"], posting["rooms"], posting.get("bedrooms"), posting.get("bathrooms"),
                posting.get("garages"), posting.get("age_years"), posting.get("publisher_type"), posting["lat"],
                posting["lng"], posting["modified"], BASE_URL + (posting["url"] or ""), usd_ars,
            ])
    # Room groups of a type whose page 1 is the whole market: counted from its listings
    for (operation, property_type), page in first_pages.items():
        if page["total"] > PAGE_SIZE:
            continue
        groups = {}
        for posting in page["postings"]:
            group = room_group(posting["rooms"])
            if group and posting["type"] == PROPERTY_TYPES[property_type]:
                groups[group] = groups.get(group, 0) + 1
        for rooms, count in sorted(groups.items()):
            totals.append([zone["slug"], operation, property_type, rooms, "", count, location, slug,
                           BASE_URL + listing_path(property_type, operation, slug, ROOM_GROUPS[rooms]),
                           page["scraped_at"]])
    for operation, key, page in scraper.filter_pages(slug, operation_totals):
        url = BASE_URL + listing_path(ALL_TYPES, operation, slug, FILTERS[operation][key], sort=False)
        totals.append([zone["slug"], operation, "all", 0, key, page["total"], location, slug, url,
                       page["scraped_at"]])
    return (totals, listings), location


def export(zones, cache_dir, usd_ars):
    """Writes totals.csv and listings.csv from the cached pages of the matched zones."""
    totals_rows, listing_rows, unmatched_rows = [], [], []
    scraper = Scraper(cache_dir, 0, download=False)
    for zone in zones:
        rows, location = zone_rows(scraper, zone, usd_ars)
        if rows is None:
            if location:
                unmatched_rows.append([zone["slug"], zone["name"], zone["parent_name"], location])
            continue
        totals_rows.extend(rows[0])
        listing_rows.extend(rows[1])
    write_csv("totals.csv", TOTALS_HEADER, totals_rows)
    write_csv("listings.csv", LISTINGS_HEADER, listing_rows)
    write_csv("unmatched.csv", ["zone_slug", "name", "parent_name", "zonaprop_answer"], unmatched_rows)
    matched = len({row[0] for row in totals_rows})
    print(f"Exportado ({cache_dir.name}): {matched} zonas con datos, {len(listing_rows)} avisos de muestra, "
          f"{len(unmatched_rows)} zonas sin equivalente en Zonaprop (unmatched.csv). Dolar: {usd_ars}.")


def write_csv(name, header, rows):
    with (DATA_DIR / name).open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(header)
        writer.writerows(rows)


def main():
    parser = argparse.ArgumentParser(description="Scraper de publicaciones de Zonaprop por zona")
    parser.add_argument("--delay", type=float, default=3.5, help="segundos promedio entre pedidos")
    parser.add_argument("--max-age-days", type=float, default=7,
                        help="dias que se sigue completando la misma corrida antes de empezar una nueva")
    parser.add_argument("--run", default="", help="carpeta de la corrida en datos/zonaprop/cache (AAAA-MM-DD)")
    parser.add_argument("--only", default="", help="slugs separados por coma")
    parser.add_argument("--usd-ars", type=float, help="pesos por dolar (por defecto, el MEP del dia)")
    parser.add_argument("--export-only", action="store_true", help="solo reconstruir los CSV desde la cache")
    args = parser.parse_args()

    only = {slug.strip() for slug in args.only.split(",") if slug.strip()}
    zones = read_zones(only)
    cache_dir = run_folder(DATA_DIR / "cache", args.run, timedelta(days=args.max_age_days))
    print(f"Corrida: {cache_dir}", flush=True)
    if not args.export_only:
        scraper = Scraper(cache_dir, args.delay)
        started = time.time()
        try:
            for index, zone in enumerate(zones, 1):
                matched, location = scraper.scrape_zone(zone)
                status = f"ok ({location})" if matched else f"sin equivalente ({location or 'otra ubicacion'})"
                minutes = (time.time() - started) / 60
                print(f"[{index}/{len(zones)}] {zone['slug']}: {status} - {scraper.requests} pedidos, "
                      f"{minutes:.0f} min", flush=True)
        except Blocked as error:
            print(f"Zonaprop sigue bloqueando ({error}). Se corta aca; volver a ejecutar mas tarde para retomar.")
            sys.exit(2)
        except KeyboardInterrupt:
            print("Interrumpido; volver a ejecutar para retomar.")
            sys.exit(130)
    export(zones, cache_dir, usd_rate(args.usd_ars))


if __name__ == "__main__":
    main()
