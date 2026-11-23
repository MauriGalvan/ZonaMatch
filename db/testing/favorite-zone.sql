\set ON_ERROR_STOP on

BEGIN;

DO $$
BEGIN
    IF current_database() <> 'zonamatch_e2e' THEN
        RAISE EXCEPTION 'The favorite-zone fixture requires the zonamatch_e2e database.';
    END IF;
END;
$$;

CREATE EXTENSION IF NOT EXISTS postgis;
CREATE SCHEMA IF NOT EXISTS geo;
CREATE SCHEMA IF NOT EXISTS osm;

CREATE TABLE IF NOT EXISTS geo.zonas (
    osm_id bigint PRIMARY KEY,
    nombre text NOT NULL,
    slug text NOT NULL,
    nivel integer NOT NULL,
    superficie_km2 double precision NOT NULL,
    geom geometry(MultiPolygon, 4326) NOT NULL,
    elegida boolean NOT NULL DEFAULT false
);

-- Keep the OSM columns read by the zone detail and existence queries.
CREATE TABLE IF NOT EXISTS osm.planet_osm_polygon (
    osm_id bigint PRIMARY KEY,
    name text NOT NULL,
    admin_level text NOT NULL,
    boundary text NOT NULL,
    way geometry(Polygon, 3857) NOT NULL,
    way_area double precision NOT NULL
);

WITH fixture AS (
    SELECT ST_Transform(ST_MakeEnvelope(-58.44, -34.59, -58.42, -34.57, 4326), 3857) AS way
)
INSERT INTO osm.planet_osm_polygon (osm_id, name, admin_level, boundary, way, way_area)
SELECT -1, 'Zona de prueba E2E', '10', 'administrative', way, ST_Area(way)
FROM fixture
ON CONFLICT (osm_id) DO NOTHING;

INSERT INTO geo.zonas (osm_id, nombre, slug, nivel, superficie_km2, geom, elegida)
SELECT osm_id, name, 'zona-e2e', 10, ST_Area(ST_Transform(way, 4326)::geography) / 1e6,
       ST_Multi(ST_Transform(way, 4326)), true
FROM osm.planet_osm_polygon
WHERE osm_id = -1
ON CONFLICT (osm_id) DO NOTHING;

COMMIT;
