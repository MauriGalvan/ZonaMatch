-- Public transit route shapes for the map layer (geo schema).
-- ZONE-DATA-DOCKER.bat runs this after loading datos/transporte_recorridos.csv (etl/process_gtfs.py) into
-- geo.carga_transporte_recorridos. Route names follow etl/calculate_transit.sql, so the layer and the zone summary
-- show the same lines ("110", not "110A").

\set ON_ERROR_STOP on
SET client_min_messages = warning;

BEGIN;

DROP TABLE IF EXISTS geo.transporte_recorridos;

-- One row per route and shape (each direction or branch), simplified to about 5 m.
CREATE TABLE geo.transporte_recorridos (
    modo          text NOT NULL,
    linea         text NOT NULL,
    recorrido_id  text NOT NULL,
    geom          geometry(LineString, 4326) NOT NULL
);

INSERT INTO geo.transporte_recorridos
SELECT c.modo,
       CASE
           WHEN c.modo = 'colectivo' AND c.linea ~ '^[0-9]+' THEN substring(c.linea FROM '^[0-9]+')
           WHEN c.modo = 'tren' THEN regexp_replace(c.linea, '^Linea ', '')
           ELSE c.linea
       END,
       c.recorrido_id,
       ST_SimplifyPreserveTopology(ST_GeomFromText(c.geom_wkt, 4326), 0.00005)
FROM geo.carga_transporte_recorridos c
WHERE c.geom_wkt IS NOT NULL;

CREATE INDEX ON geo.transporte_recorridos USING gist (geom);

DROP TABLE geo.carga_transporte_recorridos;

COMMIT;

SELECT modo, count(DISTINCT linea) AS lineas, count(*) AS recorridos
FROM geo.transporte_recorridos
GROUP BY modo
ORDER BY modo;
