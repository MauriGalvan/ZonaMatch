-- Load tables for the Zonaprop CSV files (datos/zonaprop/totals.csv and listings.csv). Run before \copy.
-- See etl/zonaprop/scrape_zonaprop.py for the meaning of each column.
\set ON_ERROR_STOP on
DROP TABLE IF EXISTS geo.load_zonaprop_totals;
CREATE TABLE geo.load_zonaprop_totals (
    zone_slug         text NOT NULL,
    operation         text NOT NULL,
    property_type     text NOT NULL,     -- departamentos, casas, ph; 'all' for the filter totals
    rooms             integer NOT NULL,  -- 0 every room count; 1 to 5 (5 or more) a room group
    filter            text,              -- empty, or credit, owner, pets
    listings          integer NOT NULL,
    zonaprop_location text,
    zonaprop_slug     text NOT NULL,
    url               text,
    scraped_at        timestamptz NOT NULL
);
DROP TABLE IF EXISTS geo.load_zonaprop_listings;
CREATE TABLE geo.load_zonaprop_listings (
    zone_slug         text NOT NULL,
    operation         text NOT NULL,
    property_type     text NOT NULL,
    stratum           integer NOT NULL,  -- room group the listing was sampled from; 0 = page 1 had the whole market
    posting_id        text NOT NULL,
    currency          text,
    price             numeric,
    expenses          numeric,
    expenses_currency text,
    total_m2          numeric,
    covered_m2        numeric,
    rooms             numeric,
    bedrooms          numeric,
    bathrooms         numeric,
    garages           numeric,
    age_years         numeric,
    publisher_type    text,              -- owner, agency
    lat               double precision,
    lng               double precision,
    modified          text,
    url               text,
    usd_ars           numeric NOT NULL
);
