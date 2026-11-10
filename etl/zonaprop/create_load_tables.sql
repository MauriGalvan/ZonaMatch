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
-- Sale listings eligible for a mortgage ("apto crédito") of each segment, in total and up to a few of its own prices
-- (scrape_zonaprop.py, credit_segments.csv). property_type: departamentos, casas, ph, or all for the three together;
-- rooms: 0 every size, 1 to 4, 5 = 5 or more; max_price_usd empty: the total of the segment.
DROP TABLE IF EXISTS geo.load_zonaprop_credit;
DROP TABLE IF EXISTS geo.load_zonaprop_credit_segments;
CREATE TABLE geo.load_zonaprop_credit_segments (
    zone_slug     text NOT NULL,
    property_type text NOT NULL,
    rooms         integer NOT NULL,
    max_price_usd numeric,
    listings      integer NOT NULL,
    -- Empty for a few segments with no listings that the scrape filled in without reading a page
    scraped_at    timestamptz
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
