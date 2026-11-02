-- Public transit frequencies by zone and time window on a weekday (geo schema).
-- DATOS-ZONAS-DOCKER.bat runs this after loading datos/transporte_salidas.csv (etl/procesar_gtfs.py) into
-- geo.carga_transporte_salidas. Requires geo.zonas (etl/calcular_zonas.sql).

\set ON_ERROR_STOP on
SET client_min_messages = warning;

BEGIN;

DROP TABLE IF EXISTS geo.zona_transporte_lineas, geo.transporte_salidas;

-- Departures for each route at each stop. For buses, the route is the number ("110") and the branch is the
-- suffix ("110A"); train and subway feeds already provide route names.
CREATE TABLE geo.transporte_salidas (
    modo       text NOT NULL,
    parada_id  text NOT NULL,
    parada     text,
    linea      text NOT NULL,
    franja     text NOT NULL,
    salidas    integer NOT NULL,
    geom       geometry(Point, 4326) NOT NULL
);

INSERT INTO geo.transporte_salidas
SELECT modo, parada_id, min(parada), normalized_route_name, franja, sum(salidas)::int,
       ST_SetSRID(ST_MakePoint(min(longitud), min(latitud)), 4326)
FROM (
    SELECT c.*,
           CASE
               WHEN c.modo = 'colectivo' AND c.linea ~ '^[0-9]+' THEN substring(c.linea FROM '^[0-9]+')
               WHEN c.modo = 'tren' THEN regexp_replace(c.linea, '^Linea ', '')
               ELSE c.linea
           END AS normalized_route_name
    FROM geo.carga_transporte_salidas c
    WHERE c.latitud IS NOT NULL AND c.longitud IS NOT NULL
) s (modo, parada_id, parada, latitud, longitud, original_route_name, franja, salidas, normalized_route_name)
GROUP BY modo, parada_id, normalized_route_name, franja;

CREATE INDEX ON geo.transporte_salidas USING gist (geom);

-- By zone, route, and time window: departures at the busiest stop in the zone (the same bus can pass through
-- several stops in a zone, so it is counted only once). Stop count: number of stops for the route in the zone.
CREATE TABLE geo.zona_transporte_lineas (
    osm_id   bigint NOT NULL REFERENCES geo.zonas (osm_id),
    modo     text NOT NULL,
    linea    text NOT NULL,
    franja   text NOT NULL,
    salidas  integer NOT NULL,
    paradas  integer NOT NULL,
    PRIMARY KEY (osm_id, modo, linea, franja)
);

INSERT INTO geo.zona_transporte_lineas
SELECT z.osm_id, s.modo, s.linea, s.franja, max(s.salidas), count(DISTINCT s.parada_id)
FROM geo.zonas z
JOIN geo.transporte_salidas s ON ST_Contains(z.geom, s.geom)
GROUP BY z.osm_id, s.modo, s.linea, s.franja;

-- Stops for each mode in the zone, regardless of time window.
DROP TABLE IF EXISTS geo.zona_transporte_paradas;
CREATE TABLE geo.zona_transporte_paradas (
    osm_id   bigint NOT NULL REFERENCES geo.zonas (osm_id),
    modo     text NOT NULL,
    paradas  integer NOT NULL,
    lineas   integer NOT NULL,
    PRIMARY KEY (osm_id, modo)
);

INSERT INTO geo.zona_transporte_paradas
SELECT z.osm_id, s.modo, count(DISTINCT s.parada_id), count(DISTINCT s.linea)
FROM geo.zonas z
JOIN geo.transporte_salidas s ON ST_Contains(z.geom, s.geom)
GROUP BY z.osm_id, s.modo;

DROP TABLE geo.carga_transporte_salidas;

COMMIT;

SELECT z.nombre, p.modo, p.paradas, p.lineas
FROM geo.zona_transporte_paradas p JOIN geo.zonas z USING (osm_id)
WHERE z.elegida AND z.slug IN ('villa-luro', 'palermo', 'villa-luzuriaga')
ORDER BY 1, 2;
