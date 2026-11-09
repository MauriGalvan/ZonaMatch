"""
Tests of the Zonaprop scraper that need no network: URLs, title matching and how the cached pages become the rows of
totals.csv and listings.csv (every page of each type). Run from backend/ZonaMatch:
  python -m unittest discover -s etl/zonaprop -p "test_*.py"
"""

import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import scrape_zonaprop as scraper  # noqa: E402


def posting(posting_id, rooms, price=100_000, publisher="2", type_name="Departamentos", operation="venta"):
    return {
        "id": str(posting_id), "type": type_name,
        "operations": {operation: {"currency": "USD", "amount": price}},
        "expenses": 50_000, "expenses_currency": "$", "total_m2": 50.0, "covered_m2": 45.0, "rooms": rooms,
        "bedrooms": None, "bathrooms": 1.0, "garages": None, "age_years": 20.0,
        "publisher_type": scraper.PUBLISHER_TYPES.get(publisher), "lat": -34.6, "lng": -58.4, "modified": None,
        "url": f"/propiedades/{posting_id}.html",
    }


def page(total, postings, title=""):
    return {"title": title, "total": total, "total_pages": (total + 29) // 30, "postings": postings,
            "final_url": None, "scraped_at": "2026-10-30T12:00:00+00:00"}


class ListingPathTests(unittest.TestCase):
    def test_listing_path_with_rooms_and_page(self):
        self.assertEqual(
            "/departamentos-venta-palermo-2-ambientes-orden-publicado-descendente-pagina-2.html",
            scraper.listing_path("departamentos", "venta", "palermo", "2-ambientes", page=2))

    def test_listing_path_of_a_filter_total_has_no_sort(self):
        self.assertEqual(
            "/departamentos-casas-ph-venta-palermo-con-apto-credito.html",
            scraper.listing_path(scraper.ALL_TYPES, "venta", "palermo", "con-apto-credito", sort=False))

    def test_price_band_segment_and_split(self):
        self.assertEqual("50000-99999-dolar", scraper.price_segment("venta", 50_000, 99_999))
        self.assertEqual(70_710, scraper.split_point(50_000, 99_999))
        self.assertEqual(7, scraper.split_point(7, 8))
        # The median of the prices seen in the band, ignoring the ones outside it
        self.assertEqual(60_000, scraper.split_point(50_000, 99_999, [55_000, 60_000, 90_000, 400_000]))
        self.assertEqual(99_998, scraper.split_point(50_000, 99_999, [99_999]))

    def test_room_group_caps_at_five(self):
        self.assertEqual([None, 1, 3, 5, 5], [scraper.room_group(r) for r in (None, 1.0, 3.0, 5.0, 8.0)])


class LocationTests(unittest.TestCase):
    zone = {"name": "Palermo", "in_caba": True, "manual_slug": ""}

    def test_title_with_filters_still_matches(self):
        title = "151 Departamentos en venta dueño directo en Palermo, CABA - Zonaprop"
        self.assertEqual((True, "Palermo, CABA"), scraper.location_matches(title, self.zone))

    def test_other_province_does_not_match(self):
        title = "3 Departamentos más recientes en venta en Palermo, Córdoba - Zonaprop"
        self.assertFalse(scraper.location_matches(title, self.zone)[0])


class ZoneRowsTests(unittest.TestCase):
    """A zone with 65 apartments for sale in 3 pages and 2 houses for sale (one page)."""

    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.cache = Path(self.folder.name)
        self.zone = {"slug": "palermo", "name": "Palermo", "in_caba": True, "manual_slug": "", "parent_name": "",
                     "candidates": ["palermo"]}
        title = "65 Departamentos más recientes en venta en Palermo, CABA - Zonaprop"
        self.write(scraper.listing_path("departamentos", "venta", "palermo"),
                   page(65, [posting(i, 2.0) for i in range(30)], title))
        # Page 2 repeats the last listing of page 1 (it moved while reading) and has one without a price
        second = [posting(i, 3.0 if i < 40 else 1.0) for i in range(29, 59)]
        second[-1]["operations"] = {}
        self.write(scraper.listing_path("departamentos", "venta", "palermo", page=2), page(65, second))
        self.write(scraper.listing_path("departamentos", "venta", "palermo", page=3),
                   page(65, [posting(i, None) for i in range(59, 64)]))
        self.write(scraper.listing_path("casas", "venta", "palermo"),
                   page(2, [posting(9001, 4.0, type_name="Casas"), posting(9002, None, type_name="Casas")]))
        self.write(scraper.listing_path("ph", "venta", "palermo"), page(0, []))
        for property_type in scraper.PROPERTY_TYPES:
            self.write(scraper.listing_path(property_type, "alquiler", "palermo"), page(0, []))
        self.write(scraper.listing_path(scraper.ALL_TYPES, "venta", "palermo", "con-apto-credito", sort=False),
                   page(40, []))
        self.write(scraper.listing_path(scraper.ALL_TYPES, "venta", "palermo", "dueno-directo", sort=False),
                   page(3, []))

    def tearDown(self):
        self.folder.cleanup()

    def write(self, path, data):
        (self.cache / (path.strip("/").replace(".html", "") + ".json")).write_text(json.dumps(data), encoding="utf-8")

    def rows(self):
        """(totals, listings) of the zone."""
        return self.all_rows()[:2]

    def all_rows(self):
        reader = scraper.Scraper(self.cache, 0, download=False)
        return scraper.zone_rows(reader, self.zone, 1500)[0]

    def test_every_page_is_read_and_repeated_listings_count_once(self):
        _, listings = self.rows()
        apartments = [row for row in listings if row[2] == "departamentos"]
        # 64 different listings, one of them without a price
        self.assertEqual(63, len(apartments))
        self.assertEqual({0}, {row[3] for row in apartments})

    def test_room_groups_are_counted_from_the_listings(self):
        totals, _ = self.rows()
        apartments = {row[3]: row[5] for row in totals if row[1] == "venta" and row[2] == "departamentos"}
        self.assertEqual({0: 65, 1: 19, 2: 30, 3: 10}, apartments)
        houses = [(row[3], row[5]) for row in totals if row[1] == "venta" and row[2] == "casas"]
        self.assertEqual([(0, 2), (4, 1)], houses)

    def test_a_page_sent_back_to_the_first_one_ends_the_type(self):
        moved = page(65, [posting(500, 2.0)])
        moved["final_url"] = scraper.BASE_URL + scraper.listing_path("departamentos", "venta", "palermo")
        self.write(scraper.listing_path("departamentos", "venta", "palermo", page=2), moved)
        _, listings = self.rows()
        self.assertEqual(30, len([row for row in listings if row[2] == "departamentos"]))

    def test_a_type_larger_than_one_search_is_read_by_price_bands(self):
        def band(low, high, total, ids):
            path = scraper.listing_path("ph", "venta", "palermo", scraper.price_segment("venta", low, high))
            self.write(path, page(total, [posting(i, 3.0, type_name="PH") for i in ids]))

        self.write(scraper.listing_path("ph", "venta", "palermo"),
                   page(300, [posting(i, 3.0, type_name="PH") for i in range(5000, 5030)]))
        # The first band has more than one search shows: it is split in two (its own listings repeat in the halves)
        # Its newest listings cost 100.000 (posting()), outside the band: it is split halfway on a log scale
        band(1, 49_999, 280, range(7000, 7003))
        band(1, 223, 10, range(7000, 7010))
        band(224, 49_999, 20, range(7010, 7030))
        band(50_000, 99_999, 5, range(7030, 7035))
        _, listings = self.rows()
        ids = {row[4] for row in listings if row[2] == "ph"}
        # page 1 of the type plus the bands, each listing once
        self.assertEqual(30 + 35, len(ids))

    def test_filter_totals_only_for_operations_with_listings(self):
        totals, _ = self.rows()
        filters = {(row[1], row[4]): row[5] for row in totals if row[4]}
        self.assertEqual({("venta", "credit"): 40, ("venta", "owner"): 3}, filters)

    def test_credit_totals_are_read_at_the_zone_sale_prices(self):
        # Every sale listing of the fixture costs US$ 100.000, so the zone has a single threshold
        self.write(scraper.credit_path("palermo", 100_000), page(35, []))
        _, _, credit = self.all_rows()
        self.assertEqual([["palermo", 100_000, 35, "2026-10-30T12:00:00+00:00"]], credit)

    def test_credit_totals_missing_from_the_cache_are_reported_as_incomplete(self):
        _, _, credit = self.all_rows()
        self.assertIsNone(credit)

    def test_credit_thresholds_are_the_zone_prices_at_fixed_shares(self):
        prices = [price * 1000 for price in range(1, 101)]
        self.assertEqual([6000, 11000, 26000, 51000, 76000, 91000, 96000], scraper.credit_thresholds(prices))
        self.assertEqual([], scraper.credit_thresholds([]))


if __name__ == "__main__":
    unittest.main()
