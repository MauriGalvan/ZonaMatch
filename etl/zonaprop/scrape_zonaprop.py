"""
Scrapes Zonaprop listing pages to measure the real estate market of each ZonaMatch zone.

For every zone in datos/zonaprop/zones.csv (exported by target_zones.sql), every operation (venta, alquiler) and every
property type (departamentos, casas, ph) it reads every published listing, not a sample. Page 1 of the type gives
the total number of active listings. Zonaprop only serves the first 9 pages of a search (270 listings, 30 per page,
newest first; later pages answer with a Cloudflare challenge), so:
  - a type with 270 listings or fewer is read page by page;
  - a larger one is split into price bands ('...-50000-99999-dolar'), and every band that still has more than 270
    listings is split in two at the median price of its newest listings, until each part can be read whole. The
    bands start at 1: listings without a price (or priced 0) are left out.
Listings that show up twice (they moved to the next page while it read, or fall in two searches) count once, by their
id. The totals of each room group (1, 2, 3, 4 and 5 or more rooms) are counted from the listings themselves.
It also reads the totals of four Zonaprop filters, for the three types together: sale listings eligible for a mortgage
("apto crédito"), sale and rent listings published by the owner ("dueño directo") and rentals that accept pets.
Sales eligible for a mortgage are also read by segment, the same way the scoring splits the market: each property type
and the three together, every size and each room group. For each segment it reads its total and the total up to a few
of its own prices (CREDIT_POINTS of the segment's sale listings in dollars), so the scoring can tell how many of them
fit a purchase cap. Segments with fewer than MIN_CREDIT_SAMPLE such listings are skipped.

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
  listings.csv  one row per published listing with a price. stratum is always 0 (the whole market, weight 1 in
                calculate_market.sql); earlier scrapes sampled by room group
  credit_segments.csv  zone_slug, property_type, rooms, max_price_usd, listings, scraped_at: sale listings eligible
                for a mortgage ("apto crédito") by segment. property_type 'all' for the three types together, rooms 0
                for every size; max_price_usd empty for the segment's total, otherwise the listings priced up to it.
                Only the total in the title of each search is read. (credit_totals.csv, the zone totals for the three
                types together, came from the first credit scrape of 2026-11-01 and is no longer written.)
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
import math
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
# Zonaprop answers pages past the 9th with a Cloudflare challenge: a search shows 270 listings at most
MAX_PAGES = 9
SEARCH_LIMIT = MAX_PAGES * PAGE_SIZE
# Shares of a segment's sale listings (by price) where its credit-eligible total is read: 5, 10, 25, 50, 75, 90, 95 %
CREDIT_POINTS = (0.05, 0.10, 0.25, 0.50, 0.75, 0.90, 0.95)
# Credit-eligible segments: each property type and 'all' (the three together), by room group (0 = every size)
CREDIT_TYPES = {"all": ALL_TYPES, **{key: key for key in PROPERTY_TYPES}}
# Segments with fewer sale listings in dollars are skipped; the API also ignores room data below this sample
MIN_CREDIT_SAMPLE = 5
# Price filter of each operation: its URL unit and where a larger search is first split (each band ends just below the
# next cut; the last cut is a ceiling no listing reaches). Sale in dollars, rent in pesos per month
PRICE_UNITS = {"venta": "dolar", "alquiler": "pesos"}
# Currency of each unit in the listing data
PRICE_CURRENCIES = {"dolar": "USD", "pesos": "$"}
PRICE_CUTS = {
    "venta": [50_000, 100_000, 200_000, 400_000, 1_000_000, 1_000_000_000],
    "alquiler": [300_000, 600_000, 1_200_000, 3_000_000, 1_000_000_000_000],
}
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


def price_segment(operation, low, high):
    """'50000-99999-dolar': the listings priced from low to high, both included."""
    return f"{low}-{high}-{PRICE_UNITS[operation]}"


def credit_thresholds(sale_prices_usd):
    """Prices of a segment at CREDIT_POINTS of its sale listings, rounded to US$ 1.000, without repeats."""
    prices = sorted(price for price in sale_prices_usd if price and price > 0)
    if not prices:
        return []
    points = {int(round(prices[min(len(prices) - 1, int(share * len(prices)))] / 1000)) * 1000 for share in CREDIT_POINTS}
    return sorted(point for point in points if point > 0)


def credit_path(property_type, slug, rooms=0, max_price=None):
    """'/departamentos-venta-palermo-con-apto-credito-2-ambientes-menos-120000-dolar.html'. Without a price, the
    segment's total. property_type 'all' is the three types together (the same page as the credit filter total) and
    rooms 0 every size."""
    segment = FILTERS["venta"]["credit"]
    if rooms:
        segment += f"-{ROOM_GROUPS[rooms]}"
    if max_price:
        segment += f"-menos-{max_price}-dolar"
    return listing_path(CREDIT_TYPES[property_type], "venta", slug, segment, sort=False)


def credit_segments(sale_listings):
    """{(property_type, rooms): prices} of a zone from (property_type, room group or None, price in dollars) of its
    sale listings: each type and 'all', every size (0) and each room group, with at least MIN_CREDIT_SAMPLE prices.
    Sorted so 'all' and every size come before the segments inside them."""
    segments = {}
    for property_type, rooms, price in sale_listings:
        for key in ("all", property_type):
            for group in {0, rooms or 0}:
                segments.setdefault((key, group), []).append(price)
    return {segment: prices for segment, prices in sorted(segments.items()) if len(prices) >= MIN_CREDIT_SAMPLE}


def credit_rows(scraper, zone_slug, slug, sale_listings):
    """credit_segments.csv rows of a zone: each segment's credit-eligible total and the total up to each of its
    prices. Pages that cannot add anything are not requested: a segment inside one without credit-eligible listings
    has none either, and once a price reaches the segment's total the higher ones are the same.
    Returns (rows, complete); complete is False when a page is missing from the cache."""
    rows = []
    totals = {}
    for (property_type, rooms), prices in credit_segments(sale_listings).items():
        if 0 in (totals.get(("all", 0)), totals.get((property_type, 0)), totals.get(("all", rooms))):
            totals[(property_type, rooms)] = 0
            rows.append([zone_slug, property_type, rooms, "", 0, ""])
            continue
        total = scraper.get(credit_path(property_type, slug, rooms))
        if total is None:
            return rows, False
        totals[(property_type, rooms)] = total["total"]
        rows.append([zone_slug, property_type, rooms, "", total["total"], total["scraped_at"]])
        if not total["total"]:
            continue
        reached = False
        for max_price in credit_thresholds(prices):
            if reached:
                rows.append([zone_slug, property_type, rooms, max_price, total["total"], total["scraped_at"]])
                continue
            page = scraper.get(credit_path(property_type, slug, rooms, max_price))
            if page is None:
                return rows, False
            rows.append([zone_slug, property_type, rooms, max_price, page["total"], page["scraped_at"]])
            reached = page["total"] >= total["total"]
    return rows, True


def sale_listings(page, property_type, seen):
    """(property_type, room group, price) of the sale listings of a page priced in dollars, each one once (as in
    listings.csv: only listings of the page's type)."""
    for posting in page["postings"]:
        price = posting["operations"].get("venta")
        key = (property_type, posting["id"])
        if posting["type"] != PROPERTY_TYPES[property_type] or key in seen:
            continue
        if not price or price.get("currency") != "USD" or not price.get("amount"):
            continue
        seen.add(key)
        yield property_type, room_group(posting["rooms"]), price["amount"]


def band_prices(page, operation):
    """Prices of the listings of a band page in the unit of its filter (sale in dollars, rent in pesos)."""
    currency = PRICE_CURRENCIES[PRICE_UNITS[operation]]
    prices = []
    for posting in page["postings"]:
        price = posting["operations"].get(operation)
        if price and price["currency"] == currency and price["amount"]:
            prices.append(price["amount"])
    return prices


def split_point(low, high, prices=()):
    """Where a price band is split in two (low..result and result + 1..high): at the median of the prices seen in it
    (its newest listings), or halfway on a log scale when there are none (prices spread out as they grow)."""
    inside = sorted(price for price in prices if low <= price <= high)
    if inside:
        middle = int(inside[(len(inside) - 1) // 2])
    else:
        middle = int(math.sqrt(low * high)) if low > 0 else high // 2
    return min(max(middle, low), high - 1)


def is_page(page, number):
    """False when Zonaprop answered a page with another one (the final URL has no '-pagina-<number>')."""
    return not page.get("final_url") or f"-pagina-{number}." in page["final_url"]


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
                print(f"    Zonaprop did not respond ({error}); waiting {waits[attempt] // 60} min...", flush=True)
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
        """Yields (operation, property_type, first, page) for every listing page of a matched zone.

        first is True for page 1 of the type, the one that gives its total. A page missing from the cache when not
        downloading ends its search.
        """
        for operation in OPERATIONS:
            for property_type in PROPERTY_TYPES:
                first = self.get(listing_path(property_type, operation, slug))
                if first is None:
                    continue
                yield operation, property_type, True, first
                if first["total"] <= SEARCH_LIMIT:
                    pages = self.search_pages(property_type, operation, slug, "", first)
                else:
                    pages = self.split_pages(property_type, operation, slug)
                for page in pages:
                    yield operation, property_type, False, page

    def search_pages(self, property_type, operation, slug, segment, first):
        """Yields the pages of a search after its first one, up to the last one Zonaprop serves.

        They end at the last page the first one announces, at an empty page, or when Zonaprop answers with another
        page (it sends past-the-end pages back to the first one).
        """
        last = min(first["total_pages"] or -(-first["total"] // PAGE_SIZE), MAX_PAGES)
        for number in range(2, last + 1):
            page = self.get(listing_path(property_type, operation, slug, segment, page=number))
            if page is None or not page["postings"] or not is_page(page, number):
                return
            yield page

    def split_pages(self, property_type, operation, slug):
        """Yields every page of a type with more listings than one search shows, band by band of price."""
        low = 1
        for cut in PRICE_CUTS[operation]:
            yield from self.band_pages(property_type, operation, slug, low, cut - 1)
            low = cut

    def band_pages(self, property_type, operation, slug, low, high):
        """Yields the pages of the listings priced from low to high (both included), splitting the band in two while
        it has more listings than one search shows. A band that cannot be split any more is read as far as it goes."""
        segment = price_segment(operation, low, high)
        first = self.get(listing_path(property_type, operation, slug, segment))
        if first is None or not first["total"]:
            return
        yield first
        if first["total"] > SEARCH_LIMIT and high > low:
            middle = split_point(low, high, band_prices(first, operation))
            yield from self.band_pages(property_type, operation, slug, low, middle)
            yield from self.band_pages(property_type, operation, slug, middle + 1, high)
            return
        yield from self.search_pages(property_type, operation, slug, segment, first)

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
        sales, seen = [], set()
        for operation, property_type, first, page in self.pages(slug):
            if first:
                totals[operation] = totals.get(operation, 0) + page["total"]
            if operation == "venta":
                sales.extend(sale_listings(page, property_type, seen))
        for _ in self.filter_pages(slug, totals):
            pass
        if totals.get("venta"):
            credit_rows(self, zone["slug"], slug, sales)
        return True, location


def slugify(text):
    """'Partido de Morón' -> 'moron', the way Zonaprop builds its location slugs."""
    return normalize(text).replace(" ", "-")


def read_zones(only):
    file = DATA_DIR / "zones.csv"
    if not file.exists():
        sys.exit(f"Missing {file}: export it with etl/zonaprop/target_zones.sql (see ZONE-MARKET-DOCKER.bat).")
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
        sys.exit(f"Could not retrieve the exchange rate ({error}); provide it with --usd-ars.")


def room_group(rooms):
    """Room group of a listing from its own number of rooms (1-5, 5 = 5 or more), None when unknown."""
    if rooms is None or rooms < 1:
        return None
    return min(int(rooms), 5)


TOTALS_HEADER = ["zone_slug", "operation", "property_type", "rooms", "filter", "listings", "zonaprop_location",
                 "zonaprop_slug", "url", "scraped_at"]
CREDIT_SEGMENTS_HEADER = ["zone_slug", "property_type", "rooms", "max_price_usd", "listings", "scraped_at"]
LISTINGS_HEADER = ["zone_slug", "operation", "property_type", "stratum", "posting_id", "currency", "price",
                   "expenses", "expenses_currency", "total_m2", "covered_m2", "rooms", "bedrooms", "bathrooms",
                   "garages", "age_years", "publisher_type", "lat", "lng", "modified", "url", "usd_ars"]


def zone_rows(scraper, zone, usd_ars):
    """totals.csv and listings.csv rows of a zone from the cache, or (None, answer) when it did not match."""
    slug, location = scraper.resolve(zone)
    if not slug:
        return None, location
    totals, listings, credit = [], [], []
    operation_totals = {}
    sales, sale_seen = [], set()
    seen = set()
    # (operation, property_type) -> (page 1, {room group: listings})
    types = {}
    for operation, property_type, first, page in scraper.pages(slug):
        type_name = PROPERTY_TYPES[property_type]
        if first:
            url = BASE_URL + listing_path(property_type, operation, slug)
            totals.append([zone["slug"], operation, property_type, 0, "", page["total"], location, slug, url,
                           page["scraped_at"]])
            operation_totals[operation] = operation_totals.get(operation, 0) + page["total"]
            types[(operation, property_type)] = (page, {})
        groups = types[(operation, property_type)][1]
        if operation == "venta":
            sales.extend(sale_listings(page, property_type, sale_seen))
        for posting in page["postings"]:
            key = (operation, property_type, posting["id"])
            if posting["type"] != type_name or key in seen:
                continue
            seen.add(key)
            group = room_group(posting["rooms"])
            if group:
                groups[group] = groups.get(group, 0) + 1
            price = posting["operations"].get(operation)
            if not price:
                continue
            # stratum 0: every listing stands for itself (calculate_market.sql gives it weight 1)
            listings.append([
                zone["slug"], operation, property_type, 0, posting["id"], price["currency"], price["amount"],
                posting.get("expenses"), posting.get("expenses_currency"), posting["total_m2"],
                posting["covered_m2"], posting["rooms"], posting.get("bedrooms"), posting.get("bathrooms"),
                posting.get("garages"), posting.get("age_years"), posting.get("publisher_type"), posting["lat"],
                posting["lng"], posting["modified"], BASE_URL + (posting["url"] or ""), usd_ars,
            ])
    # Room groups: counted from the listings of the type
    for (operation, property_type), (page, groups) in types.items():
        for rooms, count in sorted(groups.items()):
            totals.append([zone["slug"], operation, property_type, rooms, "", count, location, slug,
                           BASE_URL + listing_path(property_type, operation, slug, ROOM_GROUPS[rooms]),
                           page["scraped_at"]])
    for operation, key, page in scraper.filter_pages(slug, operation_totals):
        url = BASE_URL + listing_path(ALL_TYPES, operation, slug, FILTERS[operation][key], sort=False)
        totals.append([zone["slug"], operation, "all", 0, key, page["total"], location, slug, url,
                       page["scraped_at"]])
    if operation_totals.get("venta"):
        credit, complete = credit_rows(scraper, zone["slug"], slug, sales)
        # A page missing from the cache: this run did not read the credit totals (or not all of them)
        if not complete:
            credit = None
    return (totals, listings, credit), location


def export(zones, cache_dir, usd_ars):
    """Writes totals.csv and listings.csv from the cached pages of the matched zones."""
    totals_rows, listing_rows, credit_segment_rows, unmatched_rows = [], [], [], []
    credit_complete = True
    scraper = Scraper(cache_dir, 0, download=False)
    for zone in zones:
        rows, location = zone_rows(scraper, zone, usd_ars)
        if rows is None:
            if location:
                unmatched_rows.append([zone["slug"], zone["name"], zone["parent_name"], location])
            continue
        totals_rows.extend(rows[0])
        listing_rows.extend(rows[1])
        if rows[2] is None:
            credit_complete = False
        else:
            credit_segment_rows.extend(rows[2])
    write_csv("totals.csv", TOTALS_HEADER, totals_rows)
    write_csv("listings.csv", LISTINGS_HEADER, listing_rows)
    # Runs before the credit totals by segment existed (2026-11-01) get them from scrape_credit_segments.py: keep
    # that file
    if credit_complete and credit_segment_rows:
        write_csv("credit_segments.csv", CREDIT_SEGMENTS_HEADER, credit_segment_rows)
    elif not credit_complete:
        print("Credit-eligible totals are incomplete in this run's cache: credit_segments.csv was left as it was.")
    write_csv("unmatched.csv", ["zone_slug", "name", "parent_name", "zonaprop_answer"], unmatched_rows)
    matched = len({row[0] for row in totals_rows})
    print(f"Exported ({cache_dir.name}): {matched} zones with data, {len(listing_rows)} listings, "
          f"{len(unmatched_rows)} zones without a Zonaprop match (unmatched.csv). USD/ARS: {usd_ars}.")


def write_csv(name, header, rows):
    with (DATA_DIR / name).open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(header)
        writer.writerows(rows)


def main():
    parser = argparse.ArgumentParser(description="Scrape Zonaprop listings by zone")
    parser.add_argument("--delay", type=float, default=3.5, help="average delay between requests, in seconds")
    parser.add_argument("--max-age-days", type=float, default=7,
                        help="days a run keeps being completed before a new one starts")
    parser.add_argument("--run", default="", help="run folder in datos/zonaprop/cache (YYYY-MM-DD)")
    parser.add_argument("--only", default="", help="comma-separated zone slugs")
    parser.add_argument("--usd-ars", type=float, help="pesos per US dollar (defaults to today's MEP rate)")
    parser.add_argument("--export-only", action="store_true", help="rebuild CSV files from the cache only")
    args = parser.parse_args()

    only = {slug.strip() for slug in args.only.split(",") if slug.strip()}
    zones = read_zones(only)
    cache_dir = run_folder(DATA_DIR / "cache", args.run, timedelta(days=args.max_age_days))
    print(f"Run: {cache_dir}", flush=True)
    if not args.export_only:
        scraper = Scraper(cache_dir, args.delay)
        started = time.time()
        try:
            for index, zone in enumerate(zones, 1):
                matched, location = scraper.scrape_zone(zone)
                status = f"ok ({location})" if matched else f"no match ({location or 'another location'})"
                minutes = (time.time() - started) / 60
                print(f"[{index}/{len(zones)}] {zone['slug']}: {status} - {scraper.requests} requests, "
                      f"{minutes:.0f} min", flush=True)
        except Blocked as error:
            print(f"Zonaprop keeps blocking ({error}). Stopping here; run it again later to resume.")
            sys.exit(2)
        except KeyboardInterrupt:
            print("Interrupted; run it again to resume.")
            sys.exit(130)
    export(zones, cache_dir, usd_rate(args.usd_ars))


if __name__ == "__main__":
    main()
