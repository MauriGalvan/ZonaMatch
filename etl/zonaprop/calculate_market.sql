-- Real estate market of each zone from the Zonaprop scrape (etl/zonaprop/scrape_zonaprop.py).
-- Run by MERCADO-ZONAS-DOCKER.bat after loading datos/zonaprop/totals.csv and listings.csv into the load tables.
--
-- Result: geo.zone_market, one row per zone, operation (venta, alquiler) and property type (departamentos, casas,
-- ph and "todos"), with:
--   listings          active listings Zonaprop reports for the zone (scaled down when Zonaprop only knows the
--                     partido around it, see near_share)
--   sample_size       listings of the sample used for the averages (newest first, up to 60 per combination)
--   avg_rent_ars      monthly rent without expenses, in pesos (USD rents converted at usd_ars)
--   avg_expenses_ars  monthly expenses, in pesos
--   avg_price_usd     sale price, in dollars (peso prices converted at usd_ars)
--   avg_usd_m2        sale price per covered m2 (total m2 when the covered one is missing), in dollars
-- Averages are trimmed means (the cheapest and most expensive 10 % of the sample are left out when it has at least
-- 10 listings); medians are stored next to them.
--
-- source: 'zonaprop' measured for the zone; 'zonaprop-heredado' the zone has no Zonaprop location of its own and
-- takes the averages of the smallest zone that contains it (listings stays empty); 'zonaprop-suma' partidos and
-- comunas, adding up the zones they contain.
-- Finally it loads app.alquileres_zona (rent limit of the analysis), replacing the estimated rents.

\set ON_ERROR_STOP on
BEGIN;

-- 1. Sample listings with comparable prices
DROP TABLE IF EXISTS pg_temp.sample;
CREATE TEMP TABLE sample AS
SELECT l.zone_slug, l.operation, l.property_type, l.posting_id,
       CASE WHEN l.operation = 'alquiler'
            THEN CASE l.currency WHEN '$' THEN l.price WHEN 'USD' THEN l.price * l.usd_ars END END AS rent_ars,
       CASE WHEN l.operation = 'alquiler' AND l.expenses_ars > 0 THEN l.expenses_ars END AS expenses_ars,
       CASE WHEN l.operation = 'venta'
            THEN CASE l.currency WHEN 'USD' THEN l.price WHEN '$' THEN l.price / l.usd_ars END END AS price_usd,
       CASE WHEN coalesce(l.covered_m2, l.total_m2) BETWEEN 15 AND 3000
            THEN coalesce(l.covered_m2, l.total_m2) END AS m2,
       CASE WHEN l.lat IS NOT NULL AND l.lng IS NOT NULL
            THEN ST_SetSRID(ST_MakePoint(l.lng, l.lat), 4326) END AS point
FROM geo.load_zonaprop_listings l
WHERE l.price > 0;

-- Prices outside a plausible range are typing errors (a sale published as a rent, a total price as a m2 price...)
DELETE FROM sample
WHERE (operation = 'alquiler' AND (rent_ars IS NULL OR rent_ars NOT BETWEEN 50000 AND 30000000))
   OR (operation = 'venta' AND (price_usd IS NULL OR price_usd NOT BETWEEN 5000 AND 20000000));

-- Listings placed more than 2 km away from the zone belong to another one (or the location is wrong)
ALTER TABLE sample ADD COLUMN near boolean;
UPDATE sample s
SET near = ST_DWithin(z.geom::geography, s.point::geography, 2000)
FROM geo.zonas z
WHERE z.slug = s.zone_slug AND z.elegida AND s.point IS NOT NULL;

-- A zone whose geolocated listings are mostly elsewhere matched another place with the same name: discard it
DROP TABLE IF EXISTS pg_temp.rejected;
CREATE TEMP TABLE rejected AS
SELECT zone_slug
FROM sample
WHERE near IS NOT NULL
GROUP BY zone_slug
HAVING count(*) >= 10 AND avg(near::int) < 0.5;

-- Zonaprop sometimes only knows the whole partido (Berazategui, Pilar...): its total is scaled down by the share of
-- the sample that falls in the zone (per combination when it has 10 geolocated listings, otherwise for the zone)
DROP TABLE IF EXISTS pg_temp.near_share;
CREATE TEMP TABLE near_share AS
SELECT t.zone_slug, t.operation, t.property_type,
       coalesce(c.share, z.share, 1) AS share
FROM geo.load_zonaprop_totals t
LEFT JOIN (
    SELECT zone_slug, operation, property_type, avg(near::int) AS share
    FROM sample WHERE near IS NOT NULL
    GROUP BY zone_slug, operation, property_type HAVING count(*) >= 10
) c USING (zone_slug, operation, property_type)
LEFT JOIN (
    SELECT zone_slug, avg(near::int) AS share
    FROM sample WHERE near IS NOT NULL
    GROUP BY zone_slug HAVING count(*) >= 10
) z USING (zone_slug);

DELETE FROM sample WHERE near = false OR zone_slug IN (SELECT zone_slug FROM rejected);

-- 2. Statistics per zone, operation and type
DROP TABLE IF EXISTS pg_temp.ranked;
CREATE TEMP TABLE ranked AS
SELECT s.*,
       count(*) OVER w AS n,
       percent_rank() OVER (PARTITION BY zone_slug, operation, property_type
                            ORDER BY CASE WHEN operation = 'alquiler' THEN rent_ars ELSE price_usd / m2 END) AS pr
FROM sample s
WINDOW w AS (PARTITION BY zone_slug, operation, property_type);

CREATE TABLE IF NOT EXISTS geo.zone_market (
    osm_id           bigint NOT NULL REFERENCES geo.zonas (osm_id),
    zone_slug        text NOT NULL,
    operation        text NOT NULL CHECK (operation IN ('venta', 'alquiler')),
    property_type    text NOT NULL CHECK (property_type IN ('departamentos', 'casas', 'ph', 'todos')),
    listings         integer,
    sample_size      integer NOT NULL DEFAULT 0,
    avg_rent_ars     numeric(14, 0),
    median_rent_ars  numeric(14, 0),
    avg_expenses_ars numeric(14, 0),
    avg_price_usd    numeric(14, 0),
    avg_usd_m2       numeric(10, 0),
    median_usd_m2    numeric(10, 0),
    source           text NOT NULL,
    inherited_from   text,
    scraped_at       timestamptz NOT NULL,
    PRIMARY KEY (osm_id, operation, property_type)
);
TRUNCATE geo.zone_market;

WITH stats AS (
    SELECT zone_slug, operation, property_type,
           count(*) AS sample_size,
           avg(rent_ars) FILTER (WHERE n < 10 OR pr BETWEEN 0.1 AND 0.9) AS avg_rent_ars,
           percentile_cont(0.5) WITHIN GROUP (ORDER BY rent_ars) AS median_rent_ars,
           avg(expenses_ars) AS avg_expenses_ars,
           avg(price_usd) FILTER (WHERE n < 10 OR pr BETWEEN 0.1 AND 0.9) AS avg_price_usd,
           avg(price_usd / m2) FILTER (WHERE m2 IS NOT NULL AND (n < 10 OR pr BETWEEN 0.1 AND 0.9)) AS avg_usd_m2,
           percentile_cont(0.5) WITHIN GROUP (ORDER BY price_usd / m2) AS median_usd_m2
    FROM ranked
    GROUP BY zone_slug, operation, property_type
)
INSERT INTO geo.zone_market (osm_id, zone_slug, operation, property_type, listings, sample_size, avg_rent_ars,
                             median_rent_ars, avg_expenses_ars, avg_price_usd, avg_usd_m2, median_usd_m2, source,
                             scraped_at)
SELECT z.osm_id, t.zone_slug, t.operation, t.property_type, round(t.listings * n.share)::int, coalesce(s.sample_size, 0),
       s.avg_rent_ars, s.median_rent_ars, s.avg_expenses_ars, s.avg_price_usd, s.avg_usd_m2, s.median_usd_m2,
       'zonaprop', t.scraped_at
FROM geo.load_zonaprop_totals t
JOIN geo.zonas z ON z.slug = t.zone_slug AND z.elegida
JOIN near_share n USING (zone_slug, operation, property_type)
LEFT JOIN stats s USING (zone_slug, operation, property_type)
WHERE t.zone_slug NOT IN (SELECT zone_slug FROM rejected);

-- "todos": every type together, each type's average weighted by its number of listings
INSERT INTO geo.zone_market (osm_id, zone_slug, operation, property_type, listings, sample_size, avg_rent_ars,
                             median_rent_ars, avg_expenses_ars, avg_price_usd, avg_usd_m2, median_usd_m2, source,
                             scraped_at)
SELECT osm_id, zone_slug, operation, 'todos', sum(listings), sum(sample_size),
       sum(avg_rent_ars * listings) / nullif(sum(listings) FILTER (WHERE avg_rent_ars IS NOT NULL), 0),
       sum(median_rent_ars * listings) / nullif(sum(listings) FILTER (WHERE median_rent_ars IS NOT NULL), 0),
       sum(avg_expenses_ars * listings) / nullif(sum(listings) FILTER (WHERE avg_expenses_ars IS NOT NULL), 0),
       sum(avg_price_usd * listings) / nullif(sum(listings) FILTER (WHERE avg_price_usd IS NOT NULL), 0),
       sum(avg_usd_m2 * listings) / nullif(sum(listings) FILTER (WHERE avg_usd_m2 IS NOT NULL), 0),
       sum(median_usd_m2 * listings) / nullif(sum(listings) FILTER (WHERE median_usd_m2 IS NOT NULL), 0),
       'zonaprop', min(scraped_at)
FROM geo.zone_market
GROUP BY osm_id, zone_slug, operation;

-- 3. Zones without a Zonaprop location of their own take the averages of the smallest measured zone around them
INSERT INTO geo.zone_market (osm_id, zone_slug, operation, property_type, listings, sample_size, avg_rent_ars,
                             median_rent_ars, avg_expenses_ars, avg_price_usd, avg_usd_m2, median_usd_m2, source,
                             inherited_from, scraped_at)
SELECT c.osm_id, c.slug, m.operation, m.property_type, NULL, m.sample_size, m.avg_rent_ars, m.median_rent_ars,
       m.avg_expenses_ars, m.avg_price_usd, m.avg_usd_m2, m.median_usd_m2, 'zonaprop-heredado', p.slug, m.scraped_at
FROM geo.zonas c
CROSS JOIN LATERAL (
    SELECT z.osm_id, z.slug
    FROM geo.zonas z
    WHERE z.elegida AND z.nivel > 5 AND z.osm_id <> c.osm_id AND z.superficie_km2 > c.superficie_km2
      AND ST_Contains(z.geom, ST_PointOnSurface(c.geom))
      AND EXISTS (SELECT 1 FROM geo.zone_market m WHERE m.osm_id = z.osm_id AND m.source = 'zonaprop')
    ORDER BY z.superficie_km2
    LIMIT 1
) p
JOIN geo.zone_market m ON m.osm_id = p.osm_id
WHERE c.elegida AND c.nivel > 5
  AND NOT EXISTS (SELECT 1 FROM geo.zone_market x WHERE x.osm_id = c.osm_id);

-- 4. Partidos and comunas add up the outermost measured zones inside them (a measured zone inside another measured
-- zone is already counted by it)
WITH measured AS (
    SELECT DISTINCT z.osm_id, z.superficie_km2, z.geom, ST_PointOnSurface(z.geom) AS point
    FROM geo.zonas z
    JOIN geo.zone_market m ON m.osm_id = z.osm_id AND m.source = 'zonaprop'
    WHERE z.nivel > 5
),
members AS (
    SELECT p.osm_id AS parent_id, p.slug AS parent_slug, z.osm_id
    FROM geo.zonas p
    JOIN measured z ON z.superficie_km2 < p.superficie_km2 AND ST_Contains(p.geom, z.point)
    WHERE p.elegida AND p.nivel = 5
      AND NOT EXISTS (
          SELECT 1 FROM measured y
          WHERE y.osm_id <> z.osm_id AND y.superficie_km2 > z.superficie_km2 AND y.superficie_km2 < p.superficie_km2
            AND ST_Contains(y.geom, z.point))
)
INSERT INTO geo.zone_market (osm_id, zone_slug, operation, property_type, listings, sample_size, avg_rent_ars,
                             median_rent_ars, avg_expenses_ars, avg_price_usd, avg_usd_m2, median_usd_m2, source,
                             scraped_at)
SELECT b.parent_id, b.parent_slug, m.operation, m.property_type, sum(m.listings), sum(m.sample_size),
       sum(m.avg_rent_ars * m.listings) / nullif(sum(m.listings) FILTER (WHERE m.avg_rent_ars IS NOT NULL), 0),
       sum(m.median_rent_ars * m.listings) / nullif(sum(m.listings) FILTER (WHERE m.median_rent_ars IS NOT NULL), 0),
       sum(m.avg_expenses_ars * m.listings) / nullif(sum(m.listings) FILTER (WHERE m.avg_expenses_ars IS NOT NULL), 0),
       sum(m.avg_price_usd * m.listings) / nullif(sum(m.listings) FILTER (WHERE m.avg_price_usd IS NOT NULL), 0),
       sum(m.avg_usd_m2 * m.listings) / nullif(sum(m.listings) FILTER (WHERE m.avg_usd_m2 IS NOT NULL), 0),
       sum(m.median_usd_m2 * m.listings) / nullif(sum(m.listings) FILTER (WHERE m.median_usd_m2 IS NOT NULL), 0),
       'zonaprop-suma', min(m.scraped_at)
FROM members b
JOIN geo.zone_market m ON m.osm_id = b.osm_id
GROUP BY b.parent_id, b.parent_slug, m.operation, m.property_type;

-- 5. Rent used by the analysis (limit and available properties). Inherited zones keep their previous number of
-- available properties, since the listings of the zone around them are not theirs.
SELECT to_regclass('app.alquileres_zona') IS NOT NULL AS has_rent_table \gset
\if :has_rent_table
INSERT INTO app.alquileres_zona (zona_slug, alquiler_promedio, propiedades_disponibles, fuente, fecha_actualizacion)
SELECT zone_slug, round(avg_rent_ars, -3), coalesce(listings, 0), source, scraped_at
FROM geo.zone_market
WHERE operation = 'alquiler' AND property_type = 'todos' AND avg_rent_ars IS NOT NULL
ON CONFLICT (zona_slug) DO UPDATE
    SET alquiler_promedio = excluded.alquiler_promedio,
        propiedades_disponibles = CASE WHEN excluded.fuente = 'zonaprop-heredado'
                                       THEN app.alquileres_zona.propiedades_disponibles
                                       ELSE excluded.propiedades_disponibles END,
        fuente = excluded.fuente,
        fecha_actualizacion = excluded.fecha_actualizacion;
\endif

COMMIT;

SELECT source, count(DISTINCT osm_id) AS zones FROM geo.zone_market GROUP BY source ORDER BY source;
SELECT zone_slug AS rejected_zone FROM rejected;
