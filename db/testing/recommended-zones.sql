\set ON_ERROR_STOP on

BEGIN;

DO $$
BEGIN
    IF current_database() <> 'zonamatch_e2e' THEN
        RAISE EXCEPTION 'The recommendation fixture requires the zonamatch_e2e database.';
    END IF;
    IF NOT EXISTS (SELECT FROM geo.zona_indicadores WHERE osm_id = -2) THEN
        RAISE EXCEPTION 'Load analysis-zones.sql before the recommendation fixture.';
    END IF;
END;
$$;

-- A point in the bottom middle cell reaches five neighbors without changing the original two-zone ranking.
WITH cells AS (
    SELECT cell,
           -58.34 + (cell % 3) * 0.01 AS longitude,
           -34.66 + (cell / 3) * 0.01 AS latitude
    FROM generate_series(0, 5) AS cell
), polygons AS (
    SELECT -100 - cell AS osm_id,
           'Zona de recomendación E2E ' || (cell + 1) AS name,
           ST_Transform(ST_MakeEnvelope(longitude, latitude, longitude + 0.01, latitude + 0.01, 4326), 3857) AS way
    FROM cells
)
INSERT INTO osm.planet_osm_polygon (osm_id, name, admin_level, boundary, way, way_area)
SELECT osm_id, name, '10', 'administrative', way, ST_Area(way)
FROM polygons
ON CONFLICT (osm_id) DO NOTHING;

INSERT INTO geo.zonas (osm_id, nombre, slug, nivel, superficie_km2, geom, elegida)
SELECT osm_id, name, 'zona-recomendada-e2e-' || (-99 - osm_id), 10,
       ST_Area(ST_Transform(way, 4326)::geography) / 1e6, ST_Multi(ST_Transform(way, 4326)), true
FROM osm.planet_osm_polygon
WHERE osm_id BETWEEN -105 AND -100
ON CONFLICT (osm_id) DO NOTHING;

INSERT INTO geo.zona_indicadores (osm_id, poblacion, delitos_comparables, hogares, hogares_alquilan)
SELECT osm_id, 10000, 40 + (-100 - osm_id) * 70, 4000, 1000
FROM geo.zonas
WHERE osm_id BETWEEN -105 AND -100
ON CONFLICT (osm_id) DO NOTHING;

COMMIT;
