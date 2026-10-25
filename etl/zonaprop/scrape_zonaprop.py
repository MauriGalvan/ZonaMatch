"""
Scrapes Zonaprop listing pages to measure the real estate market of each ZonaMatch zone.

For every zone in datos/zonaprop/zones.csv (exported by target_zones.sql) and every combination of operation
(venta, alquiler) and property type (departamentos, casas, ph) it reads:
  - the total number of active listings Zonaprop reports for that location, and
  - a sample of the most recent listings (price, currency, expenses, surface, coordinates).

Zonaprop resolves its location URLs by slug, which usually matches ours ("villa-crespo", "adrogue"). An unknown
slug falls back to a wider search and an ambiguous one may land in another province, so every response is
checked against the expected location (its name, in CABA or in an AMBA partido) before it is used. Names repeated
elsewhere in the country are tried again with the partido ("castelar-moron"). Zones that still do not match are
logged in datos/zonaprop/unmatched.csv; slug_overrides.csv maps them by hand (a Zonaprop slug, or the partido of
zones that geo.zonas does not place in one).

Outputs (in datos/zonaprop/):
  totals.csv    zone_slug, operation, property_type, listings, zonaprop_location, zonaprop_slug, url, scraped_at
  listings.csv  one row per sampled listing
Statistics per zone are computed in SQL (etl/zonaprop/calculate_market.sql), which also drops listings whose
coordinates fall far from the zone.

The scraper is deliberately slow: one request every few seconds. When Zonaprop answers with a challenge or an
error it waits and retries a few times, and then stops. Every response is cached, so running it again resumes
where it stopped.

Usage (from backend/ZonaMatch):
  python etl/zonaprop/scrape_zonaprop.py                 scrape everything still missing
  python etl/zonaprop/scrape_zonaprop.py --pages 1       smaller sample (30 listings per combination)
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
from datetime import datetime, timedelta, timezone
from pathlib import Path

BASE_URL = "https://www.zonaprop.com.ar"
DATA_DIR = Path(__file__).resolve().parents[2] / "datos" / "zonaprop"
CACHE_DIR = DATA_DIR / "cache"
OVERRIDES_FILE = Path(__file__).resolve().parent / "slug_overrides.csv"

OPERATIONS = ["venta", "alquiler"]
# Zonaprop URL segment -> property type name in the listing data
PROPERTY_TYPES = {"departamentos": "Departamentos", "casas": "Casas", "ph": "PH"}
# Newest first: the default "relevance" order favors paid listings
SORT_SUFFIX = "-orden-publicado-descendente"
USD_RATE_URL = "https://dolarapi.com/v1/dolares/bolsa"

USER_AGENT = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) "
    "Chrome/130.0 Safari/537.36"
)
STATE_MARKER = "window.__PRELOADED_STATE__ = "
TITLE_PATTERN = re.compile(r"<title>\s*([^<]*?)\s*</title>", re.S)
# "1.234 Departamentos más recientes en venta en Adrogué, Almirante Brown - Pagina 2 - Zonaprop"
LOCATION_PATTERN = re.compile(r" en (?:venta|alquiler) en (.+?)(?: - P[aá]gina \d+)? - Zonaprop$")


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


def fetch(path, delay):
    """Downloads a listing page and returns (final_url, html). Raises Blocked on challenges."""
    time.sleep(delay * random.uniform(0.7, 1.4))
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
    except (urllib.error.URLError, TimeoutError) as error:
        raise Blocked(str(error)) from error
    if STATE_MARKER not in html:
        raise Blocked("page without listing data (challenge?)")
    return final_url, html


def parse_page(html):
    """Extracts the title, the total and the minimal fields of each listing from a Zonaprop page."""
    title_match = TITLE_PATTERN.search(html)
    title = title_match.group(1) if title_match else ""
    start = html.index(STATE_MARKER) + len(STATE_MARKER)
    state, _ = json.JSONDecoder().raw_decode(html, start)
    store = state["listStore"]
    paging = store.get("paging") or {}
    total = int(paging.get("total") or 0)
    postings = []
    for posting in store.get("listPostings") or [] if total > 0 else []:
        postings.append(parse_posting(posting))
    return {"title": title, "total": total, "total_pages": paging.get("totalPages") or 0, "postings": postings}


def number(value):
    try:
        return float(str(value).replace(",", "."))
    except (TypeError, ValueError):
        return None


def parse_posting(posting):
    features = posting.get("mainFeatures") or {}
    location = posting.get("postingLocation") or {}
    geo = (location.get("postingGeolocation") or {}).get("geolocation") or {}
    expenses = posting.get("expenses") or {}
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
        "expenses": expenses.get("amount") if expenses.get("currency") in ("$", None) else None,
        "total_m2": number((features.get("CFT100") or {}).get("value")),
        "covered_m2": number((features.get("CFT101") or {}).get("value")),
        "rooms": number((features.get("CFT1") or {}).get("value")),
        "lat": geo.get("latitude"),
        "lng": geo.get("longitude"),
        "location_name": (location.get("location") or {}).get("name"),
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


def listing_path(property_type, operation, slug, page):
    page_part = f"-pagina-{page}" if page > 1 else ""
    return f"/{property_type}-{operation}-{slug}{SORT_SUFFIX}{page_part}.html"


class Scraper:
    def __init__(self, delay, max_age_days):
        self.delay = delay
        self.max_age = timedelta(days=max_age_days)
        self.requests = 0
        CACHE_DIR.mkdir(parents=True, exist_ok=True)

    def cache_file(self, path):
        return CACHE_DIR / (path.strip("/").replace(".html", "") + ".json")

    def get(self, path):
        """Returns the parsed page, from the cache when it is recent enough."""
        file = self.cache_file(path)
        if file.exists():
            cached = json.loads(file.read_text(encoding="utf-8"))
            if datetime.now(timezone.utc) - datetime.fromisoformat(cached["scraped_at"]) < self.max_age:
                return cached
        waits = [60, 180, 600]
        for attempt in range(len(waits) + 1):
            try:
                final_url, html = fetch(path, self.delay)
                break
            except NotFound:
                # Cached too, so a slug Zonaprop does not know is asked only once
                self.requests += 1
                page = {"title": "", "total": 0, "total_pages": 0, "postings": [], "final_url": None,
                        "scraped_at": datetime.now(timezone.utc).isoformat(timespec="seconds")}
                file.write_text(json.dumps(page), encoding="utf-8")
                return page
            except Blocked as error:
                if attempt == len(waits):
                    raise
                print(f"    Zonaprop no respondio ({error}); esperando {waits[attempt]} s...", flush=True)
                time.sleep(waits[attempt])
        self.requests += 1
        page = parse_page(html)
        page["final_url"] = final_url
        page["scraped_at"] = datetime.now(timezone.utc).isoformat(timespec="seconds")
        file.write_text(json.dumps(page, ensure_ascii=False), encoding="utf-8")
        return page

    def cached(self, path):
        """Returns the cached page, or None, without downloading anything."""
        file = self.cache_file(path)
        return json.loads(file.read_text(encoding="utf-8")) if file.exists() else None

    def resolve(self, zone, download=True):
        """Finds the Zonaprop slug of a zone, trying each candidate. Returns (slug, location) or (None, answer)."""
        answer = ""
        for slug in zone["candidates"]:
            path = listing_path("departamentos", "venta", slug, 1)
            page = self.get(path) if download else self.cached(path)
            if page is None:
                continue
            matches, location = location_matches(page["title"], zone)
            if matches:
                return slug, location
            answer = answer or location or page["title"]
        return None, answer

    def scrape_zone(self, zone, pages):
        """Scrapes every combination for a zone. Returns (False, answer) when Zonaprop does not know it."""
        slug, location = self.resolve(zone)
        if not slug:
            return False, location
        for operation in OPERATIONS:
            for property_type in PROPERTY_TYPES:
                page = self.get(listing_path(property_type, operation, slug, 1))
                for number_ in range(2, min(pages, page["total_pages"]) + 1):
                    self.get(listing_path(property_type, operation, slug, number_))
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


def export(zones, pages, usd_ars):
    """Writes totals.csv and listings.csv from the cached pages of the matched zones."""
    totals_rows, listing_rows, unmatched_rows = [], [], []
    scraper = Scraper(0, 36500)
    for zone in zones:
        slug, location = scraper.resolve(zone, download=False)
        if not slug:
            if location:
                unmatched_rows.append([zone["slug"], zone["name"], zone["parent_name"], location])
            continue
        for operation in OPERATIONS:
            for property_type, type_name in PROPERTY_TYPES.items():
                page_file = scraper.cache_file(listing_path(property_type, operation, slug, 1))
                if not page_file.exists():
                    continue
                page = json.loads(page_file.read_text(encoding="utf-8"))
                totals_rows.append([zone["slug"], operation, property_type, page["total"], location, slug,
                                    BASE_URL + listing_path(property_type, operation, slug, 1), page["scraped_at"]])
                seen = set()
                for number_ in range(1, pages + 1):
                    file = scraper.cache_file(listing_path(property_type, operation, slug, number_))
                    if not file.exists():
                        break
                    for posting in json.loads(file.read_text(encoding="utf-8"))["postings"]:
                        price = posting["operations"].get(operation)
                        if not price or posting["type"] != type_name or posting["id"] in seen:
                            continue
                        seen.add(posting["id"])
                        listing_rows.append([
                            zone["slug"], operation, property_type, posting["id"], price["currency"], price["amount"],
                            posting["expenses"], posting["total_m2"], posting["covered_m2"], posting["rooms"],
                            posting["lat"], posting["lng"], posting["modified"], BASE_URL + (posting["url"] or ""),
                            usd_ars,
                        ])
    write_csv("totals.csv", ["zone_slug", "operation", "property_type", "listings", "zonaprop_location",
                             "zonaprop_slug", "url",
                             "scraped_at"], totals_rows)
    write_csv("listings.csv", ["zone_slug", "operation", "property_type", "posting_id", "currency", "price",
                               "expenses_ars", "total_m2", "covered_m2", "rooms", "lat", "lng", "modified", "url",
                               "usd_ars"],
              listing_rows)
    write_csv("unmatched.csv", ["zone_slug", "name", "parent_name", "zonaprop_answer"], unmatched_rows)
    matched = len({row[0] for row in totals_rows})
    print(f"Exportado: {matched} zonas con datos, {len(listing_rows)} avisos de muestra, "
          f"{len(unmatched_rows)} zonas sin equivalente en Zonaprop (unmatched.csv). Dolar: {usd_ars}.")


def write_csv(name, header, rows):
    with (DATA_DIR / name).open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(header)
        writer.writerows(rows)


def main():
    parser = argparse.ArgumentParser(description="Scraper de publicaciones de Zonaprop por zona")
    parser.add_argument("--pages", type=int, default=2, help="paginas de 30 avisos por combinacion (muestra)")
    parser.add_argument("--delay", type=float, default=4.0, help="segundos promedio entre pedidos")
    parser.add_argument("--max-age-days", type=float, default=14, help="antiguedad maxima de la cache")
    parser.add_argument("--only", default="", help="slugs separados por coma")
    parser.add_argument("--usd-ars", type=float, help="pesos por dolar (por defecto, el MEP del dia)")
    parser.add_argument("--export-only", action="store_true", help="solo reconstruir los CSV desde la cache")
    args = parser.parse_args()

    only = {slug.strip() for slug in args.only.split(",") if slug.strip()}
    zones = read_zones(only)
    if not args.export_only:
        scraper = Scraper(args.delay, args.max_age_days)
        started = time.time()
        try:
            for index, zone in enumerate(zones, 1):
                matched, location = scraper.scrape_zone(zone, args.pages)
                status = f"ok ({location})" if matched else f"sin equivalente ({location or 'otra ubicacion'})"
                minutes = (time.time() - started) / 60
                print(f"[{index}/{len(zones)}] {zone['slug']}: {status} - {scraper.requests} pedidos, "
                      f"{minutes:.0f} min", flush=True)
        except Blocked as error:
            print(f"Zonaprop sigue bloqueando ({error}). Se corta aca; volver a ejecutar mas tarde para retomar.")
        except KeyboardInterrupt:
            print("Interrumpido; volver a ejecutar para retomar.")
    export(zones, args.pages, usd_rate(args.usd_ars))


if __name__ == "__main__":
    main()
