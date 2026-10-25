-- Real estate market of each zone from the Zonaprop scrape (etl/zonaprop/scrape_zonaprop.py), written to
-- app.zone_market. Run by MERCADO-ZONAS-DOCKER.bat after loading datos/zonaprop/totals.csv and listings.csv into
-- the load tables (create_load_tables.sql).
--
-- One row per zone, operation (sale, rent) and property type (apartment, house, ph, and "all" for every type):
--   listings            active listings Zonaprop reports for the zone (scaled down when Zonaprop only knows the
--                       partido around it, see near_share)
--   sample_size         listings used for the averages (newest first, up to 60 per combination)
--   average_price       rent per month without expenses in ARS, or sale price in USD (other currencies converted
--   median_price        at the usd_ars rate of the scrape)
--   average_price_m2    the same per covered m2 (total m2 when the covered one is missing)
--   median_price_m2
--   average_expenses_ars  monthly expenses of rentals
-- Averages are trimmed means: the cheapest and most expensive 10 % of the sample are left out when it has at least
-- 10 listings.
--
-- source: 'zonaprop' measured for the zone; 'zonaprop-inherited' the zone has no Zonaprop location of its own and
-- takes the averages of the smallest measured zone around it (listings stays empty); 'zonaprop-sum' partidos and
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
       CASE WHEN l.operation = 'alquiler'
            THEN CASE l.currency WHEN '$' THEN l.price WHEN 'USD' THEN l.price * l.usd_ars END
            ELSE CASE l.currency WHEN 'USD' THEN l.price WHEN '$' THEN l.price / l.usd_ars END
       END AS price,
       CASE WHEN l.operation = 'alquiler' AND l.expenses_ars > 0 THEN l.expenses_ars END AS expenses_ars,
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
-- answer (Pilar, Tigre) has a good part of its listings near the localidad and is scaled down instead (near_share)
DROP TABLE IF EXISTS pg_temp.rejected;
CREATE TEMP TABLE rejected AS
SELECT zone_slug
FROM sample
WHERE near IS NOT NULL
GROUP BY zone_slug
HAVING count(*) >= 10 AND avg(near::int) < 0.2;

-- Zonaprop sometimes answers with the whole partido (Berazategui, Morón...): its total is scaled down by the share
-- of the sample inside the zone (per combination when it has 10 geolocated listings, otherwise for the zone).
-- A Zonaprop location shared by several zones (Villa Ballester for its Noreste and Noroeste) gives them its prices
-- but no listings of their own, so partidos do not count it twice.
DROP TABLE IF EXISTS pg_temp.near_share;
CREATE TEMP TABLE near_share AS
SELECT t.zone_slug,
       CASE t.operation WHEN 'venta' THEN 'sale' ELSE 'rent' END AS operation,
       CASE t.property_type WHEN 'departamentos' THEN 'apartment' WHEN 'casas' THEN 'house' ELSE 'ph' END
           AS property_type,
       CASE WHEN count(*) OVER (PARTITION BY t.zonaprop_slug, t.operation, t.property_type) = 1 THEN t.listings END
           AS listings,
       t.listings AS zonaprop_listings,
       t.scraped_at
FROM geo.load_zonaprop_totals t
WHERE t.zone_slug NOT IN (SELECT zone_slug FROM rejected);

ALTER TABLE near_share ADD COLUMN share numeric;
UPDATE near_share n
SET share = coalesce(
    (SELECT avg(inside::int) FROM sample s
     WHERE s.zone_slug = n.zone_slug AND s.operation = n.operation AND s.property_type = n.property_type
       AND s.inside IS NOT NULL
     HAVING count(*) >= 10),
    (SELECT avg(inside::int) FROM sample s
     WHERE s.zone_slug = n.zone_slug AND s.inside IS NOT NULL
     HAVING count(*) >= 10),
    1);

DELETE FROM sample WHERE near = false OR zone_slug IN (SELECT zone_slug FROM rejected);

-- 2. Statistics per zone, operation and type
DROP TABLE IF EXISTS pg_temp.ranked;
CREATE TEMP TABLE ranked AS
SELECT s.*,
       s.price / s.m2 AS price_m2,
       count(*) OVER w AS n,
       count(s.m2) OVER w AS n_m2,
       percent_rank() OVER (w ORDER BY s.price) AS price_rank,
       CASE WHEN s.m2 IS NOT NULL
            THEN percent_rank() OVER (PARTITION BY s.zone_slug, s.operation, s.property_type, s.m2 IS NULL
                                      ORDER BY s.price / s.m2) END AS m2_rank
FROM sample s
WINDOW w AS (PARTITION BY s.zone_slug, s.operation, s.property_type);

DROP TABLE IF EXISTS pg_temp.market;
CREATE TEMP TABLE market (
    zone_slug            text NOT NULL,
    osm_id               bigint NOT NULL,
    operation            text NOT NULL,
    property_type        text NOT NULL,
    listings             integer,
    sample_size          integer NOT NULL,
    average_price        numeric,
    median_price         numeric,
    average_price_m2     numeric,
    median_price_m2      numeric,
    average_expenses_ars numeric,
    source               text NOT NULL,
    inherited_from       text,
    updated_at           timestamptz NOT NULL
);

WITH stats AS (
    SELECT zone_slug, operation, property_type,
           count(*) AS sample_size,
           avg(price) FILTER (WHERE n < 10 OR price_rank BETWEEN 0.1 AND 0.9) AS average_price,
           percentile_cont(0.5) WITHIN GROUP (ORDER BY price) AS median_price,
           avg(price_m2) FILTER (WHERE price_m2 IS NOT NULL AND (n_m2 < 10 OR m2_rank BETWEEN 0.1 AND 0.9))
               AS average_price_m2,
           percentile_cont(0.5) WITHIN GROUP (ORDER BY price_m2) AS median_price_m2,
           avg(expenses_ars) AS average_expenses_ars
    FROM ranked
    GROUP BY zone_slug, operation, property_type
)
INSERT INTO market
SELECT n.zone_slug, z.osm_id, n.operation, n.property_type, round(n.listings * n.share)::int,
       coalesce(s.sample_size, 0), s.average_price, s.median_price, s.average_price_m2, s.median_price_m2,
       s.average_expenses_ars, 'zonaprop', NULL, n.scraped_at
FROM near_share n
JOIN geo.zonas z ON z.slug = n.zone_slug AND z.elegida
LEFT JOIN stats s USING (zone_slug, operation, property_type);

-- "all": every type together, each type's value weighted by its number of listings (for zones sharing a Zonaprop
-- location, by the listings of that location, which they do not count as their own)
WITH weighted AS (
    SELECT m.*, coalesce(m.listings, round(n.zonaprop_listings * n.share)) AS w
    FROM market m
    JOIN near_share n USING (zone_slug, operation, property_type)
)
INSERT INTO market
SELECT zone_slug, osm_id, operation, 'all', sum(listings), sum(sample_size),
       sum(average_price * w) / nullif(sum(w) FILTER (WHERE average_price IS NOT NULL), 0),
       sum(median_price * w) / nullif(sum(w) FILTER (WHERE median_price IS NOT NULL), 0),
       sum(average_price_m2 * w) / nullif(sum(w) FILTER (WHERE average_price_m2 IS NOT NULL), 0),
       sum(median_price_m2 * w) / nullif(sum(w) FILTER (WHERE median_price_m2 IS NOT NULL), 0),
       sum(average_expenses_ars * w) / nullif(sum(w) FILTER (WHERE average_expenses_ars IS NOT NULL), 0),
       'zonaprop', NULL, min(updated_at)
FROM weighted
GROUP BY zone_slug, osm_id, operation;

-- 3. Zones without a Zonaprop location of their own take the averages of the smallest measured zone around them
INSERT INTO market
SELECT c.slug, c.osm_id, m.operation, m.property_type, NULL, m.sample_size, m.average_price, m.median_price,
       m.average_price_m2, m.median_price_m2, m.average_expenses_ars, 'zonaprop-inherited', p.slug, m.updated_at
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
SELECT b.parent_slug, b.parent_id, m.operation, m.property_type, sum(m.listings), sum(m.sample_size),
       sum(m.average_price * m.listings) / nullif(sum(m.listings) FILTER (WHERE m.average_price IS NOT NULL), 0),
       sum(m.median_price * m.listings) / nullif(sum(m.listings) FILTER (WHERE m.median_price IS NOT NULL), 0),
       sum(m.average_price_m2 * m.listings)
           / nullif(sum(m.listings) FILTER (WHERE m.average_price_m2 IS NOT NULL), 0),
       sum(m.median_price_m2 * m.listings)
           / nullif(sum(m.listings) FILTER (WHERE m.median_price_m2 IS NOT NULL), 0),
       sum(m.average_expenses_ars * m.listings)
           / nullif(sum(m.listings) FILTER (WHERE m.average_expenses_ars IS NOT NULL), 0),
       'zonaprop-sum', NULL, min(m.updated_at)
FROM members b
JOIN market m ON m.osm_id = b.osm_id
GROUP BY b.parent_id, b.parent_slug, m.operation, m.property_type;

-- 4b. Zones still without data (not in Zonaprop and not inside a measured zone) take the averages of their partido
-- or comuna
INSERT INTO market
SELECT c.slug, c.osm_id, m.operation, m.property_type, NULL, m.sample_size, m.average_price, m.median_price,
       m.average_price_m2, m.median_price_m2, m.average_expenses_ars, 'zonaprop-inherited', p.slug, m.updated_at
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

INSERT INTO app.zone_market (zone_slug, operation, property_type, listings, sample_size, average_price, median_price,
                             currency, average_price_m2, median_price_m2, average_expenses_ars, source,
                             inherited_from, updated_at)
SELECT DISTINCT ON (zone_slug, operation, property_type)
       zone_slug, operation, property_type, listings, sample_size,
       round(average_price, CASE operation WHEN 'rent' THEN -3 ELSE 0 END), round(median_price),
       CASE operation WHEN 'rent' THEN 'ARS' ELSE 'USD' END,
       round(average_price_m2), round(median_price_m2), round(average_expenses_ars, -2), source, inherited_from,
       updated_at
FROM market
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
        source = excluded.source,
        inherited_from = excluded.inherited_from,
        updated_at = excluded.updated_at;

COMMIT;

SELECT source, count(DISTINCT zone_slug) AS zones FROM app.zone_market GROUP BY source ORDER BY source;
SELECT zone_slug AS rejected_zone FROM rejected;
