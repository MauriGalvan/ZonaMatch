-- Loads an ESTIMATED rent into app.zone_market for each base zone (neighborhoods and localities), for all property
-- types, so rental analysis limits work for zones without Zonaprop data yet (etl/zonaprop/, MERCADO-ZONAS-DOCKER.bat).
-- DATOS-ZONAS-DOCKER.bat runs this after calculate_zones.sql. It can also be run on its own:
--   docker compose exec -T db psql -U zonamatch -d zonamatch_docker < etl\estimate_rents.sql
--
-- These are NOT actual market prices. Starting from a base CABA rent (:base, average one-bedroom apartment), the
-- estimate is adjusted by the zone's socioeconomic level based on the 2022 census (share of people with university
-- education); rents outside CABA are reduced by 25%. Available properties are estimated as 3% of renting households.
-- Only zones without data are filled; values from another source are not overwritten (source <> 'estimated').
--
-- Override the base value: psql -v base=900000 ...

\set ON_ERROR_STOP on
\if :{?base}
\else
    \set base 750000
\endif

-- The backend migration ReplaceRentByZoneMarket creates this table; without it, there is nowhere to load the data.
SELECT to_regclass('app.zone_market') IS NOT NULL AS market_table_exists \gset
\if :market_table_exists
BEGIN;

WITH caba_zone AS (
    SELECT geom FROM geo.zonas WHERE nivel = 4 ORDER BY superficie_km2 DESC LIMIT 1
),
base AS (
    SELECT z.slug,
           i.hogares_alquilan,
           i.personas_universitario::numeric / nullif(i.personas_con_dato_educacion, 0) AS university_education_share,
           EXISTS (SELECT 1 FROM caba_zone c WHERE ST_Contains(c.geom, ST_PointOnSurface(z.geom))) AS within_caba
    FROM geo.zonas z
    JOIN geo.zona_indicadores i USING (osm_id)
    WHERE z.elegida AND z.nivel > 5 AND i.hogares > 0
)
INSERT INTO app.zone_market (zone_slug, operation, property_type, listings, sample_size, average_price, currency,
                             source, updated_at)
SELECT DISTINCT ON (slug)
       slug, 'rent', 'all',
       round(hogares_alquilan * 0.03)::int,
       0,
       -- Rounded to the nearest thousand pesos.
       round(:base * (0.6 + 2.5 * coalesce(university_education_share, 0.07)) * CASE WHEN within_caba THEN 1 ELSE 0.75 END, -3),
       'ARS',
       'estimated',
       now()
FROM base
ORDER BY slug
ON CONFLICT (zone_slug, operation, property_type) DO UPDATE
    SET average_price = excluded.average_price,
        listings = excluded.listings,
        updated_at = excluded.updated_at
    WHERE app.zone_market.source = 'estimated';

COMMIT;
\else
\echo 'app.zone_market is missing: apply the backend migrations (dotnet ef database update) and run this script again.'
\endif
