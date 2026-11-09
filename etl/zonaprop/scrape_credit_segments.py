"""
Provisional scrape: credit-eligible ("apto crédito") sale totals by segment for a run whose listings are already
scraped (2026-11-01), without downloading the listings again.

The full scrape (scrape_zonaprop.py) reads these totals by itself from now on; this script brings them for the data
already loaded. It reads the sale listings from datos/zonaprop/listings.csv and the Zonaprop slug of each zone from
datos/zonaprop/totals.csv, so it does not need the page cache of that run. Then, with the same functions as the full
scrape, it asks Zonaprop for each segment (each property type and the three together, every size and each room group)
its credit-eligible total and the total up to a few of its own prices, and writes datos/zonaprop/credit_segments.csv.

Pages are cached in datos/zonaprop/cache/<run>/ like the full scrape: running it again resumes where it stopped, and a
full scrape of the same run later reuses them. Once every zone is complete it writes credit_segments.csv; until then
the file is left as it was. Remove this script once a full scrape has brought these totals.

Usage (from backend/ZonaMatch):
  python etl/zonaprop/scrape_credit_segments.py --dry-run     count the requests it needs, without asking Zonaprop
  python etl/zonaprop/scrape_credit_segments.py               scrape what is still missing, then write the CSV
  python etl/zonaprop/scrape_credit_segments.py --only palermo,adrogue
  python etl/zonaprop/scrape_credit_segments.py --export-only rebuild credit_segments.csv from the cache
"""

import argparse
import csv
import sys
import time
from datetime import timedelta
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from scrape_zonaprop import (  # noqa: E402
    CREDIT_SEGMENTS_HEADER, DATA_DIR, Blocked, Scraper, credit_path, credit_rows, credit_segments, credit_thresholds,
    room_group, run_folder, write_csv,
)


def read_zones(only):
    """[(zone slug, Zonaprop slug, sale listings)] of the zones with sale listings in dollars, from the CSV files."""
    slugs = {}
    with (DATA_DIR / "totals.csv").open(encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle):
            if row["zonaprop_slug"]:
                slugs.setdefault(row["zone_slug"], row["zonaprop_slug"])
    sales = {}
    with (DATA_DIR / "listings.csv").open(encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle):
            if row["operation"] != "venta" or row["currency"] != "USD" or not row["price"]:
                continue
            price = float(row["price"])
            if price <= 0:
                continue
            rooms = room_group(float(row["rooms"])) if row["rooms"] else None
            sales.setdefault(row["zone_slug"], []).append((row["property_type"], rooms, price))
    return [(zone, slugs[zone], sales[zone]) for zone in sorted(sales)
            if zone in slugs and (not only or zone in only)]


def planned_requests(scraper, slug, sales):
    """(pages, cached) a zone needs at most: each segment's total and one page per price, before the pages that
    cannot add anything are skipped (credit_rows skips them while scraping)."""
    pages = cached = 0
    for (property_type, rooms), prices in credit_segments(sales).items():
        paths = [credit_path(property_type, slug, rooms)]
        paths += [credit_path(property_type, slug, rooms, max_price) for max_price in credit_thresholds(prices)]
        pages += len(paths)
        cached += sum(scraper.cache_file(path).exists() for path in paths)
    return pages, cached


def export(zones, cache_dir):
    """Writes credit_segments.csv from the cache when every zone is complete."""
    scraper = Scraper(cache_dir, 0, download=False)
    rows, missing = [], []
    for zone, slug, sales in zones:
        zone_rows, complete = credit_rows(scraper, zone, slug, sales)
        if complete:
            rows.extend(zone_rows)
        else:
            missing.append(zone)
    if missing:
        print(f"{len(missing)} zones still have pages to read (for example {', '.join(missing[:5])}): "
              "credit_segments.csv was left as it was. Run it again to resume.")
        return
    write_csv("credit_segments.csv", CREDIT_SEGMENTS_HEADER, rows)
    print(f"Exported ({cache_dir.name}): credit_segments.csv with {len(rows)} rows of {len(zones)} zones.")


def main():
    parser = argparse.ArgumentParser(description="Scrape the credit-eligible sale totals by segment")
    parser.add_argument("--delay", type=float, default=3.5, help="average delay between requests, in seconds")
    parser.add_argument("--max-age-days", type=float, default=7,
                        help="days a run keeps being completed before a new one starts")
    parser.add_argument("--run", default="", help="run folder in datos/zonaprop/cache (YYYY-MM-DD)")
    parser.add_argument("--only", default="", help="comma-separated zone slugs")
    parser.add_argument("--dry-run", action="store_true", help="count the requests without asking Zonaprop")
    parser.add_argument("--export-only", action="store_true", help="rebuild credit_segments.csv from the cache only")
    args = parser.parse_args()

    only = {slug.strip() for slug in args.only.split(",") if slug.strip()}
    zones = read_zones(only)
    cache_dir = run_folder(DATA_DIR / "cache", args.run, timedelta(days=args.max_age_days))
    print(f"Run: {cache_dir} - {len(zones)} zones with sale listings", flush=True)

    if args.dry_run:
        scraper = Scraper(cache_dir, 0, download=False)
        pages = cached = 0
        for _, slug, sales in zones:
            zone_pages, zone_cached = planned_requests(scraper, slug, sales)
            pages += zone_pages
            cached += zone_cached
        hours = (pages - cached) * args.delay / 3600
        print(f"At most {pages} pages, {cached} already cached: {pages - cached} requests left, about "
              f"{hours:.1f} h at {args.delay} s each (fewer: pages that cannot add anything are skipped).")
        return

    if not args.export_only:
        scraper = Scraper(cache_dir, args.delay)
        started = time.time()
        try:
            for index, (zone, slug, sales) in enumerate(zones, 1):
                credit_rows(scraper, zone, slug, sales)
                minutes = (time.time() - started) / 60
                print(f"[{index}/{len(zones)}] {zone} - {scraper.requests} requests, {minutes:.0f} min", flush=True)
        except Blocked as error:
            print(f"Zonaprop keeps blocking ({error}). Stopping here; run it again later to resume.")
            sys.exit(2)
        except KeyboardInterrupt:
            print("Interrupted; run it again to resume.")
            sys.exit(130)
    export(read_zones(set()), cache_dir)


if __name__ == "__main__":
    main()
