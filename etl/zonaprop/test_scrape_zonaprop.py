"""
Tests of the Zonaprop scraper that need no network: URLs, title matching and how the cached pages become the rows of
totals.csv and listings.csv (stratified by rooms). Run from backend/ZonaMatch:
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
    """A zone with 95 apartments for sale (stratified) and 2 houses for sale (page 1 is the whole market)."""

    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.cache = Path(self.folder.name)
        self.zone = {"slug": "palermo", "name": "Palermo", "in_caba": True, "manual_slug": "", "parent_name": "",
                     "candidates": ["palermo"]}
        title = "95 Departamentos más recientes en venta en Palermo, CABA - Zonaprop"
        self.write(scraper.listing_path("departamentos", "venta", "palermo"),
                   page(95, [posting(i, 2.0) for i in range(30)], title))
        for rooms, segment in scraper.ROOM_GROUPS.items():
            postings = [posting(1000 * rooms + i, float(rooms)) for i in range(3)] if rooms <= 2 else []
            self.write(scraper.listing_path("departamentos", "venta", "palermo", segment),
                       page(60 if rooms == 1 else 35 if rooms == 2 else 0, postings))
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
        reader = scraper.Scraper(self.cache, 0, download=False)
        return scraper.zone_rows(reader, self.zone, 1500)[0]

    def test_stratified_type_uses_the_room_pages_as_sample(self):
        _, listings = self.rows()
        apartments = [row for row in listings if row[2] == "departamentos"]
        # 3 + 3 from the room pages; page 1 is left out because it is not the whole market
        self.assertEqual([1, 1, 1, 2, 2, 2], [row[3] for row in apartments])

    def test_type_with_few_listings_counts_its_room_groups_from_page_one(self):
        totals, listings = self.rows()
        houses = [row for row in totals if row[1] == "venta" and row[2] == "casas"]
        self.assertEqual([(0, 2), (4, 1)], [(row[3], row[5]) for row in houses])
        self.assertEqual({0}, {row[3] for row in listings if row[2] == "casas"})

    def test_filter_totals_only_for_operations_with_listings(self):
        totals, _ = self.rows()
        filters = {(row[1], row[4]): row[5] for row in totals if row[4]}
        self.assertEqual({("venta", "credit"): 40, ("venta", "owner"): 3}, filters)


if __name__ == "__main__":
    unittest.main()
