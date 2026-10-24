-- Load tables for the Zonaprop CSV files (datos/zonaprop/totals.csv and listings.csv). Run before \copy.
\set ON_ERROR_STOP on
DROP TABLE IF EXISTS geo.load_zonaprop_totals;
CREATE TABLE geo.load_zonaprop_totals (
    zone_slug         text NOT NULL,
    operation         text NOT NULL,
    property_type     text NOT NULL,
    listings          integer NOT NULL,
    zonaprop_location text,
    url               text,
    scraped_at        timestamptz NOT NULL
);
DROP TABLE IF EXISTS geo.load_zonaprop_listings;
CREATE TABLE geo.load_zonaprop_listings (
    zone_slug     text NOT NULL,
    operation     text NOT NULL,
    property_type text NOT NULL,
    posting_id    text NOT NULL,
    currency      text,
    price         numeric,
    expenses_ars  numeric,
    total_m2      numeric,
    covered_m2    numeric,
    rooms         numeric,
    lat           double precision,
    lng           double precision,
    modified      text,
    url           text,
    usd_ars       numeric NOT NULL
);
