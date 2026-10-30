-- Real estate market of each zone from the Zonaprop scrape (etl/zonaprop/scrape_zonaprop.py), written to
-- app.zone_market (every room count) and app.zone_market_rooms (one row per room group). Run by
-- MERCADO-ZONAS-DOCKER.bat after loading datos/zonaprop/totals.csv and listings.csv into the load tables
-- (create_load_tables.sql).
--
-- One row per zone, operation (sale, rent), property type (apartment, house, ph, and "all" for every type) and room
-- group (rooms: 0 every room count, 1 to 4, 5 = 5 or more):
--   listings             active listings Zonaprop reports for the zone (scaled down when Zonaprop only knows the
--                        partido around it, see share)
--   sample_size          listings used for the averages
--   average_price        rent per month without expenses in ARS, or sale price in USD (other currencies converted
--   median_price         at the usd_ars rate of the scrape)
--   average_price_m2     the same per covered m2 (total m2 when the covered one is missing)
--   median_price_m2
--   average_expenses_ars monthly expenses of rentals
--   median_covered_m2    typical surface of the listings
--   median_total_m2
-- Only in app.zone_market, on the "all" row of each operation (Zonaprop counts them for every type together):
--   credit_eligible_share  share of the sale listings eligible for a mortgage ("apto crédito")
--   owner_share            share of the listings published by the owner ("dueño directo")
--   pets_allowed_share     share of the rentals that accept pets
--
-- The sample is stratified by rooms: when a type has more than 30 listings the scraper reads the 30 newest of each
-- room group, so every listing weighs the listings of its group it stands for (group total / group sample). The
-- figures for every room count are therefore weighted by the real mix of the zone, not by what was published last.
-- Averages are trimmed means: the cheapest and most expensive 10 % (by weight) are left out when there are at least
-- 10 listings. Medians are weighted too.
--
-- source: 'zonaprop' measured for the zone; 'zonaprop-inherited' the zone has no Zonaprop location of its own and
-- takes the values of the smallest measured zone around it (listings stays empty); 'zonaprop-sum' partidos and
-- comunas, adding up the zones they contain. Previous Zonaprop rows are replaced and estimated rents
-- (source 'estimated') are overwritten where there is real data now.

\set ON_ERROR_STOP on
BEGIN;

-- 1. Sample listings with comparable prices
DROP TABLE IF EXISTS pg_temp.sample;
CREATE TEMP TABLE sample AS
SELECT l.zone_slug,
       CASE l.operation WHEN 'venta' THEN 'sale' ELSE 'rent' END AS operation,
       CASE l.property_type WHEN 'departamentos' THEN 'apartment' WHEN 'casas' THEN 'house' ELSE 'ph' END
           AS property_type,
       l.stratum,
       -- Room group: the one it was sampled from, or its own number of rooms when page 1 had the whole market
       CASE WHEN l.stratum > 0 THEN l.stratum WHEN l.rooms >= 1 THEN least(floor(l.rooms)::int, 5) END AS rooms,
       l.posting_id,
       CASE WHEN l.operation = 'alquiler'
            THEN CASE l.currency WHEN '$' THEN l.price WHEN 'USD' THEN l.price * l.usd_ars END
            ELSE CASE l.currency WHEN 'USD' THEN l.price WHEN '$' THEN l.price / l.usd_ars END
       END AS price,
       CASE WHEN l.operation = 'alquiler' AND l.expenses > 0 AND coalesce(l.expenses_currency, '$') = '$'
            THEN l.expenses END AS expenses_ars,
       CASE WHEN l.covered_m2 BETWEEN 15 AND 3000 THEN l.covered_m2 END AS covered_m2,
       CASE WHEN l.total_m2 BETWEEN 15 AND 20000 THEN l.total_m2 END AS total_m2,
       CASE WHEN coalesce(l.covered_m2, l.total_m2) BETWEEN 15 AND 3000
            THEN coalesce(l.covered_m2, l.total_m2) END AS m2,
       CASE WHEN l.lat IS NOT NULL AND l.lng IS NOT NULL
            THEN ST_SetSRID(ST_MakePoint(l.lng, l.lat), 4326) END AS point
FROM geo.load_zonaprop_listings l
WHERE l.price > 0;

-- Prices outside a plausible range are typing errors (a sale published as a rent, a m2 price as the total...)
DELETE FROM sample
WHERE price IS NULL
   OR (operation = 'rent' AND price NOT BETWEEN 50000 AND 30000000)
   OR (operation = 'sale' AND price NOT BETWEEN 5000 AND 20000000);

-- Listings placed more than 2 km away from the zone belong to another one (or the location is wrong). Listings
-- inside it (300 m of tolerance for approximate addresses) measure how much of a partido total is the zone's.
ALTER TABLE sample ADD COLUMN near boolean, ADD COLUMN inside boolean;
UPDATE sample s
SET near = ST_DWithin(z.geom::geography, s.point::geography, 2000),
    inside = ST_DWithin(z.geom::geography, s.point::geography, 300)
FROM geo.zonas z
WHERE z.slug = s.zone_slug AND z.elegida AND s.point IS NOT NULL;

-- A zone with almost no geolocated listings near it matched another place with the same name: discard it. A partido
-- answer (Pilar, Tigre) has a good part of its listings near the localidad and is scaled down instead (share)
DROP TABLE IF EXISTS pg_temp.rejected;
CREATE TEMP TABLE rejected AS
SELECT zone_slug
FROM sample
WHERE near IS NOT NULL
GROUP BY zone_slug
HAVING count(*) >= 10 AND avg(near::int) < 0.2;

-- Listings per zone, operation, type and room group. Zonaprop sometimes answers with the whole partido (Berazategui,
-- Morón...): its totals are scaled down by the share of the sample inside the zone (per type when it has 10
-- geolocated listings, otherwise for the zone). A Zonaprop location shared by several zones (Villa Ballester for its
-- Noreste and Noroeste) gives them its prices but no listings of their own, so partidos do not count it twice.
DROP TABLE IF EXISTS pg_temp.counts;
CREATE TEMP TABLE counts AS
SELECT t.zone_slug,
       CASE t.operation WHEN 'venta' THEN 'sale' ELSE 'rent' END AS operation,
       CASE t.property_type WHEN 'departamentos' THEN 'apartment' WHEN 'casas' THEN 'house' ELSE 'ph' END
           AS property_type,
       t.rooms,
       CASE WHEN count(*) OVER (PARTITION BY t.zonaprop_slug, t.operation, t.property_type, t.rooms) = 1
            THEN t.listings END AS own_listings,
       t.listings AS zonaprop_listings,
       t.scraped_at
FROM geo.load_zonaprop_totals t
WHERE coalesce(t.filter, '') = '' AND t.property_type <> 'all'
  AND t.zone_slug NOT IN (SELECT zone_slug FROM rejected);

ALTER TABLE counts ADD COLUMN share numeric;
UPDATE counts c
SET share = coalesce(
    (SELECT avg(inside::int) FROM sample s
     WHERE s.zone_slug = c.zone_slug AND s.operation = c.operation AND s.property_type = c.property_type
       AND s.inside IS NOT NULL
     HAVING count(*) >= 10),
    (SELECT avg(inside::int) FROM sample s
     WHERE s.zone_slug = c.zone_slug AND s.inside IS NOT NULL
     HAVING count(*) >= 10),
    1);

DELETE FROM sample WHERE near = false OR zone_slug IN (SELECT zone_slug FROM rejected);

-- Weight of each listing: 1 when page 1 had the whole market, otherwise the listings of its room group it stands for
ALTER TABLE sample ADD COLUMN w numeric;
UPDATE sample s
SET w = CASE WHEN s.stratum = 0 THEN 1 ELSE c.zonaprop_listings::numeric / g.n END
FROM counts c,
     (SELECT zone_slug, operation, property_type, stratum, count(*) AS n
      FROM sample GROUP BY zone_slug, operation, property_type, stratum) g
WHERE c.zone_slug = s.zone_slug AND c.operation = s.operation AND c.property_type = s.property_type
  AND c.rooms = s.stratum
  AND g.zone_slug = s.zone_slug AND g.operation = s.operation AND g.property_type = s.property_type
  AND g.stratum = s.stratum;
DELETE FROM sample WHERE w IS NULL OR w <= 0;

-- 2. Statistics per zone, operation, type and room group (0 = every listing, each one with its weight)
DROP TABLE IF EXISTS pg_temp.vals;
CREATE TEMP TABLE vals AS
SELECT e.zone_slug, e.operation, e.property_type, e.rooms, m.metric, m.value, e.w
FROM (
    SELECT zone_slug, operation, property_type, 0 AS rooms, price, expenses_ars, covered_m2, total_m2, m2, w FROM sample
    UNION ALL
    SELECT zone_slug, operation, property_type, rooms, price, expenses_ars, covered_m2, total_m2, m2, w
    FROM sample WHERE rooms IS NOT NULL
) e
CROSS JOIN LATERAL (VALUES ('price', e.price), ('price_m2', e.price / e.m2), ('expenses', e.expenses_ars),
                           ('covered_m2', e.covered_m2), ('total_m2', e.total_m2)) m(metric, value)
WHERE m.value IS NOT NULL;

-- Trimmed mean and median of each metric, both weighted. mid: weighted position of the value (0 to 1)
DROP TABLE IF EXISTS pg_temp.stats;
CREATE TEMP TABLE stats AS
WITH ranked AS (
    SELECT v.*,
           sum(w) OVER (PARTITION BY zone_slug, operation, property_type, rooms, metric
                        ORDER BY value ROWS UNBOUNDED PRECEDING) AS cumulative,
           sum(w) OVER (PARTITION BY zone_slug, operation, property_type, rooms, metric) AS total_w,
           count(*) OVER (PARTITION BY zone_slug, operation, property_type, rooms, metric) AS n
    FROM vals v
),
flagged AS (
    SELECT *, (n < 10 OR (cumulative - w / 2) / total_w BETWEEN 0.1 AND 0.9) AS kept
    FROM ranked
)
SELECT zone_slug, operation, property_type, rooms, metric,
       count(*) AS n,
       sum(w * value) FILTER (WHERE kept) / nullif(sum(w) FILTER (WHERE kept), 0) AS mean,
       min(value) FILTER (WHERE cumulative >= total_w / 2) AS median
FROM flagged
GROUP BY zone_slug, operation, property_type, rooms, metric;

DROP TABLE IF EXISTS pg_temp.market;
CREATE TEMP TABLE market (
    zone_slug             text NOT NULL,
    osm_id                bigint NOT NULL,
    operation             text NOT NULL,
    property_type         text NOT NULL,
    rooms                 integer NOT NULL,
    listings              integer,
    sample_size           integer NOT NULL,
    average_price         numeric,
    median_price          numeric,
    average_price_m2      numeric,
    median_price_m2       numeric,
    average_expenses_ars  numeric,
    median_covered_m2     numeric,
    median_total_m2       numeric,
    credit_eligible_share numeric,
    owner_share           numeric,
    pets_allowed_share    numeric,
    source                text NOT NULL,
    inherited_from        text,
    updated_at            timestamptz NOT NULL
);

WITH pivot AS (
    SELECT zone_slug, operation, property_type, rooms,
           max(n) FILTER (WHERE metric = 'price') AS sample_size,
           max(mean) FILTER (WHERE metric = 'price') AS average_price,
           max(median) FILTER (WHERE metric = 'price') AS median_price,
           max(mean) FILTER (WHERE metric = 'price_m2') AS average_price_m2,
           max(median) FILTER (WHERE metric = 'price_m2') AS median_price_m2,
           max(mean) FILTER (WHERE metric = 'expenses') AS average_expenses_ars,
           max(median) FILTER (WHERE metric = 'covered_m2') AS median_covered_m2,
           max(median) FILTER (WHERE metric = 'total_m2') AS median_total_m2
    FROM stats
    GROUP BY zone_slug, operation, property_type, rooms
)
INSERT INTO market (zone_slug, osm_id, operation, property_type, rooms, listings, sample_size, average_price,
                    median_price, average_price_m2, median_price_m2, average_expenses_ars, median_covered_m2,
                    median_total_m2, source, updated_at)
SELECT c.zone_slug, z.osm_id, c.operation, c.property_type, c.rooms, round(c.own_listings * c.share)::int,
       coalesce(p.sample_size, 0), p.average_price, p.median_price, p.average_price_m2, p.median_price_m2,
       p.average_expenses_ars, p.median_covered_m2, p.median_total_m2, 'zonaprop', c.scraped_at
FROM counts c
JOIN geo.zonas z ON z.slug = c.zone_slug AND z.elegida
LEFT JOIN pivot p USING (zone_slug, operation, property_type, rooms);

-- "all": every type together, each type's value weighted by its number of listings (for zones sharing a Zonaprop
-- location, by the listings of that location, which they do not count as their own)
WITH weighted AS (
    SELECT m.*, coalesce(m.listings, round(c.zonaprop_listings * c.share)) AS w
    FROM market m
    JOIN counts c USING (zone_slug, operation, property_type, rooms)
)
INSERT INTO market (zone_slug, osm_id, operation, property_type, rooms, listings, sample_size, average_price,
                    median_price, average_price_m2, median_price_m2, average_expenses_ars, median_covered_m2,
                    median_total_m2, source, updated_at)
SELECT zone_slug, osm_id, operation, 'all', rooms, sum(listings), sum(sample_size),
       sum(average_price * w) / nullif(sum(w) FILTER (WHERE average_price IS NOT NULL), 0),
       sum(median_price * w) / nullif(sum(w) FILTER (WHERE median_price IS NOT NULL), 0),
       sum(average_price_m2 * w) / nullif(sum(w) FILTER (WHERE average_price_m2 IS NOT NULL), 0),
       sum(median_price_m2 * w) / nullif(sum(w) FILTER (WHERE median_price_m2 IS NOT NULL), 0),
       sum(average_expenses_ars * w) / nullif(sum(w) FILTER (WHERE average_expenses_ars IS NOT NULL), 0),
       sum(median_covered_m2 * w) / nullif(sum(w) FILTER (WHERE median_covered_m2 IS NOT NULL), 0),
       sum(median_total_m2 * w) / nullif(sum(w) FILTER (WHERE median_total_m2 IS NOT NULL), 0),
       'zonaprop', min(updated_at)
FROM weighted
GROUP BY zone_slug, osm_id, operation, rooms;

-- Filter shares: Zonaprop totals of each filter over the listings of the three types, both for the whole Zonaprop
-- location (so a partido answer keeps its proportion)
WITH filters AS (
    SELECT t.zone_slug, CASE t.operation WHEN 'venta' THEN 'sale' ELSE 'rent' END AS operation, t.filter,
           t.listings
    FROM geo.load_zonaprop_totals t
    WHERE coalesce(t.filter, '') <> ''
),
type_totals AS (
    SELECT zone_slug, operation, sum(zonaprop_listings) AS listings
    FROM counts
    WHERE rooms = 0
    GROUP BY zone_slug, operation
),
shares AS (
    SELECT f.zone_slug, f.operation,
           max(least(f.listings::numeric / nullif(t.listings, 0), 1)) FILTER (WHERE f.filter = 'credit') AS credit,
           max(least(f.listings::numeric / nullif(t.listings, 0), 1)) FILTER (WHERE f.filter = 'owner') AS owner,
           max(least(f.listings::numeric / nullif(t.listings, 0), 1)) FILTER (WHERE f.filter = 'pets') AS pets
    FROM filters f
    JOIN type_totals t USING (zone_slug, operation)
    GROUP BY f.zone_slug, f.operation
)
UPDATE market m
SET credit_eligible_share = s.credit, owner_share = s.owner, pets_allowed_share = s.pets
FROM shares s
WHERE m.zone_slug = s.zone_slug AND m.operation = s.operation AND m.property_type = 'all' AND m.rooms = 0;

-- 3. Zones without a Zonaprop location of their own take the values of the smallest measured zone around them
INSERT INTO market
SELECT c.slug, c.osm_id, m.operation, m.property_type, m.rooms, NULL, m.sample_size, m.average_price, m.median_price,
       m.average_price_m2, m.median_price_m2, m.average_expenses_ars, m.median_covered_m2, m.median_total_m2,
       m.credit_eligible_share, m.owner_share, m.pets_allowed_share, 'zonaprop-inherited', p.slug, m.updated_at
FROM geo.zonas c
CROSS JOIN LATERAL (
    SELECT z.osm_id, z.slug
    FROM geo.zonas z
    WHERE z.elegida AND z.nivel > 5 AND z.osm_id <> c.osm_id AND z.superficie_km2 > c.superficie_km2
      AND ST_Contains(z.geom, ST_PointOnSurface(c.geom))
      AND EXISTS (SELECT 1 FROM market m WHERE m.osm_id = z.osm_id)
    ORDER BY z.superficie_km2
    LIMIT 1
) p
JOIN market m ON m.osm_id = p.osm_id
WHERE c.elegida AND c.nivel > 5
  AND NOT EXISTS (SELECT 1 FROM market x WHERE x.osm_id = c.osm_id);

-- 4. Partidos and comunas add up the outermost measured zones inside them (a measured zone inside another measured
-- zone is already counted by it)
WITH measured AS (
    SELECT DISTINCT z.osm_id, z.superficie_km2, z.geom, ST_PointOnSurface(z.geom) AS point
    FROM geo.zonas z
    JOIN market m ON m.osm_id = z.osm_id AND m.source = 'zonaprop'
    WHERE z.nivel > 5
),
members AS (
    SELECT p.osm_id AS parent_id, p.slug AS parent_slug, z.osm_id
    FROM geo.zonas p
    -- <= because some barrios are a whole comuna (Palermo is Comuna 14)
    JOIN measured z ON z.osm_id <> p.osm_id AND z.superficie_km2 <= p.superficie_km2 AND ST_Contains(p.geom, z.point)
    WHERE p.elegida AND p.nivel = 5
      AND NOT EXISTS (
          SELECT 1 FROM measured y
          WHERE y.osm_id <> z.osm_id AND y.superficie_km2 > z.superficie_km2 AND y.superficie_km2 <= p.superficie_km2
            AND ST_Contains(y.geom, z.point))
)
INSERT INTO market
SELECT b.parent_slug, b.parent_id, m.operation, m.property_type, m.rooms, sum(m.listings), sum(m.sample_size),
       sum(m.average_price * m.listings) / nullif(sum(m.listings) FILTER (WHERE m.average_price IS NOT NULL), 0),
       sum(m.median_price * m.listings) / nullif(sum(m.listings) FILTER (WHERE m.median_price IS NOT NULL), 0),
       sum(m.average_price_m2 * m.listings)
           / nullif(sum(m.listings) FILTER (WHERE m.average_price_m2 IS NOT NULL), 0),
       sum(m.median_price_m2 * m.listings)
           / nullif(sum(m.listings) FILTER (WHERE m.median_price_m2 IS NOT NULL), 0),
       sum(m.average_expenses_ars * m.listings)
           / nullif(sum(m.listings) FILTER (WHERE m.average_expenses_ars IS NOT NULL), 0),
       sum(m.median_covered_m2 * m.listings)
           / nullif(sum(m.listings) FILTER (WHERE m.median_covered_m2 IS NOT NULL), 0),
       sum(m.median_total_m2 * m.listings)
           / nullif(sum(m.listings) FILTER (WHERE m.median_total_m2 IS NOT NULL), 0),
       sum(m.credit_eligible_share * m.listings)
           / nullif(sum(m.listings) FILTER (WHERE m.credit_eligible_share IS NOT NULL), 0),
       sum(m.owner_share * m.listings) / nullif(sum(m.listings) FILTER (WHERE m.owner_share IS NOT NULL), 0),
       sum(m.pets_allowed_share * m.listings)
           / nullif(sum(m.listings) FILTER (WHERE m.pets_allowed_share IS NOT NULL), 0),
       'zonaprop-sum', NULL, min(m.updated_at)
FROM members b
JOIN market m ON m.osm_id = b.osm_id
GROUP BY b.parent_id, b.parent_slug, m.operation, m.property_type, m.rooms;

-- 4b. Zones still without data (not in Zonaprop and not inside a measured zone) take the values of their partido
-- or comuna
INSERT INTO market
SELECT c.slug, c.osm_id, m.operation, m.property_type, m.rooms, NULL, m.sample_size, m.average_price, m.median_price,
       m.average_price_m2, m.median_price_m2, m.average_expenses_ars, m.median_covered_m2, m.median_total_m2,
       m.credit_eligible_share, m.owner_share, m.pets_allowed_share, 'zonaprop-inherited', p.slug, m.updated_at
FROM geo.zonas c
JOIN LATERAL (
    SELECT z.osm_id, z.slug
    FROM geo.zonas z
    WHERE z.elegida AND z.nivel = 5 AND ST_Contains(z.geom, ST_PointOnSurface(c.geom))
      AND EXISTS (SELECT 1 FROM market m WHERE m.osm_id = z.osm_id)
    LIMIT 1
) p ON true
JOIN market m ON m.osm_id = p.osm_id
WHERE c.elegida AND c.nivel > 5
  AND NOT EXISTS (SELECT 1 FROM market x WHERE x.osm_id = c.osm_id);

-- 5. Replace the previous Zonaprop rows; real data wins over estimated rents
DELETE FROM app.zone_market WHERE source LIKE 'zonaprop%';
DELETE FROM app.zone_market_rooms;

INSERT INTO app.zone_market (zone_slug, operation, property_type, listings, sample_size, average_price, median_price,
                             currency, average_price_m2, median_price_m2, average_expenses_ars, median_covered_m2,
                             median_total_m2, credit_eligible_share, owner_share, pets_allowed_share, source,
                             inherited_from, updated_at)
SELECT DISTINCT ON (zone_slug, operation, property_type)
       zone_slug, operation, property_type, listings, sample_size,
       round(average_price, CASE operation WHEN 'rent' THEN -3 ELSE 0 END), round(median_price),
       CASE operation WHEN 'rent' THEN 'ARS' ELSE 'USD' END,
       round(average_price_m2), round(median_price_m2), round(average_expenses_ars, -2), round(median_covered_m2),
       round(median_total_m2), round(credit_eligible_share, 4), round(owner_share, 4), round(pets_allowed_share, 4),
       source, inherited_from, updated_at
FROM market
WHERE rooms = 0
ORDER BY zone_slug, operation, property_type, osm_id
ON CONFLICT (zone_slug, operation, property_type) DO UPDATE
    SET listings = excluded.listings,
        sample_size = excluded.sample_size,
        average_price = excluded.average_price,
        median_price = excluded.median_price,
        currency = excluded.currency,
        average_price_m2 = excluded.average_price_m2,
        median_price_m2 = excluded.median_price_m2,
        average_expenses_ars = excluded.average_expenses_ars,
        median_covered_m2 = excluded.median_covered_m2,
        median_total_m2 = excluded.median_total_m2,
        credit_eligible_share = excluded.credit_eligible_share,
        owner_share = excluded.owner_share,
        pets_allowed_share = excluded.pets_allowed_share,
        source = excluded.source,
        inherited_from = excluded.inherited_from,
        updated_at = excluded.updated_at;

INSERT INTO app.zone_market_rooms (zone_slug, operation, property_type, rooms, listings, sample_size, average_price,
                                   median_price, currency, average_price_m2, median_price_m2, average_expenses_ars,
                                   median_covered_m2, median_total_m2, source, inherited_from, updated_at)
SELECT DISTINCT ON (zone_slug, operation, property_type, rooms)
       zone_slug, operation, property_type, rooms, listings, sample_size,
       round(average_price, CASE operation WHEN 'rent' THEN -3 ELSE 0 END), round(median_price),
       CASE operation WHEN 'rent' THEN 'ARS' ELSE 'USD' END,
       round(average_price_m2), round(median_price_m2), round(average_expenses_ars, -2), round(median_covered_m2),
       round(median_total_m2), source, inherited_from, updated_at
FROM market
WHERE rooms > 0 AND sample_size > 0
ORDER BY zone_slug, operation, property_type, rooms, osm_id;

COMMIT;

SELECT source, count(DISTINCT zone_slug) AS zones FROM app.zone_market GROUP BY source ORDER BY source;
SELECT count(*) AS room_rows, count(DISTINCT zone_slug) AS room_zones FROM app.zone_market_rooms;
SELECT zone_slug AS rejected_zone FROM rejected;
