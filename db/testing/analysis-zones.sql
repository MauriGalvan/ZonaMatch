\set ON_ERROR_STOP on

BEGIN;

DO $$
BEGIN
    IF current_database() <> 'zonamatch_e2e' THEN
        RAISE EXCEPTION 'The analysis fixture requires the zonamatch_e2e database.';
    END IF;
    IF NOT EXISTS (SELECT FROM geo.zonas WHERE osm_id = -1 AND slug = 'zona-e2e') THEN
        RAISE EXCEPTION 'Load favorite-zone.sql before the analysis fixture.';
    END IF;
END;
$$;

-- The neighbor shares a border with the favorite zone and uses the same coordinate transforms.
WITH neighbor AS (
    SELECT ST_Transform(ST_MakeEnvelope(-58.42, -34.59, -58.40, -34.57, 4326), 3857) AS way
)
INSERT INTO osm.planet_osm_polygon (osm_id, name, admin_level, boundary, way, way_area)
SELECT -2, 'Zona vecina E2E', '10', 'administrative', way, ST_Area(way)
FROM neighbor
ON CONFLICT (osm_id) DO NOTHING;

INSERT INTO geo.zonas (osm_id, nombre, slug, nivel, superficie_km2, geom, elegida)
SELECT osm_id, name, 'zona-vecina-e2e', 10, ST_Area(ST_Transform(way, 4326)::geography) / 1e6,
       ST_Multi(ST_Transform(way, 4326)), true
FROM osm.planet_osm_polygon
WHERE osm_id = -2
ON CONFLICT (osm_id) DO NOTHING;

-- Keep the OSM columns used by scoring and the public map of approved contributions.
ALTER TABLE osm.planet_osm_polygon
    ADD COLUMN IF NOT EXISTS amenity text,
    ADD COLUMN IF NOT EXISTS leisure text,
    ADD COLUMN IF NOT EXISTS shop text,
    ADD COLUMN IF NOT EXISTS tourism text,
    ADD COLUMN IF NOT EXISTS railway text,
    ADD COLUMN IF NOT EXISTS public_transport text,
    ADD COLUMN IF NOT EXISTS highway text,
    ADD COLUMN IF NOT EXISTS operator text;

CREATE TABLE IF NOT EXISTS osm.planet_osm_point (
    name text,
    amenity text,
    leisure text,
    way geometry(Point, 3857)
);

ALTER TABLE osm.planet_osm_point
    ADD COLUMN IF NOT EXISTS osm_id bigint,
    ADD COLUMN IF NOT EXISTS shop text,
    ADD COLUMN IF NOT EXISTS tourism text,
    ADD COLUMN IF NOT EXISTS railway text,
    ADD COLUMN IF NOT EXISTS public_transport text,
    ADD COLUMN IF NOT EXISTS highway text,
    ADD COLUMN IF NOT EXISTS operator text;

CREATE TABLE IF NOT EXISTS geo.zona_indicadores (
    osm_id bigint PRIMARY KEY REFERENCES geo.zonas (osm_id),
    poblacion integer NOT NULL,
    delitos_comparables integer NOT NULL,
    hogares integer NOT NULL,
    hogares_alquilan integer NOT NULL
);

INSERT INTO geo.zona_indicadores (osm_id, poblacion, delitos_comparables, hogares, hogares_alquilan)
VALUES (-1, 10000, 20, 4000, 1000), (-2, 10000, 200, 4000, 1000)
ON CONFLICT (osm_id) DO NOTHING;

-- No transport data is required to rank the two zones by safety and estimated travel times.
CREATE TABLE IF NOT EXISTS geo.zona_transporte_paradas (
    osm_id bigint NOT NULL REFERENCES geo.zonas (osm_id),
    lineas integer NOT NULL
);

CREATE TABLE IF NOT EXISTS geo.zona_transporte_lineas (
    osm_id bigint NOT NULL REFERENCES geo.zonas (osm_id),
    franja text NOT NULL,
    salidas integer NOT NULL
);

COMMIT;
