-- Frecuencias de transporte publico por zona y franja horaria de un dia habil (schema geo).
-- Lo corre DATOS-ZONAS-DOCKER.bat despues de copiar datos/transporte_salidas.csv (etl/procesar_gtfs.py) en
-- geo.carga_transporte_salidas. Necesita geo.zonas (etl/calcular_zonas.sql).

\set ON_ERROR_STOP on
SET client_min_messages = warning;

BEGIN;

DROP TABLE IF EXISTS geo.zona_transporte_lineas, geo.transporte_salidas;

-- Salidas de cada linea en cada parada. Colectivos: la linea es el numero ("110") y el ramal lo que sigue
-- ("110A"); trenes y subte ya vienen por linea.
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
SELECT modo, parada_id, min(parada), linea, franja, sum(salidas)::int,
       ST_SetSRID(ST_MakePoint(min(longitud), min(latitud)), 4326)
FROM (
    SELECT c.*,
           CASE
               WHEN c.modo = 'colectivo' AND c.linea ~ '^[0-9]+' THEN substring(c.linea FROM '^[0-9]+')
               WHEN c.modo = 'tren' THEN regexp_replace(c.linea, '^Linea ', '')
               ELSE c.linea
           END AS linea_normalizada
    FROM geo.carga_transporte_salidas c
    WHERE c.latitud IS NOT NULL AND c.longitud IS NOT NULL
) s (modo, parada_id, parada, latitud, longitud, linea_original, franja, salidas, linea)
GROUP BY modo, parada_id, linea, franja;

CREATE INDEX ON geo.transporte_salidas USING gist (geom);

-- Por zona, linea y franja: las salidas en la parada de la zona por donde pasan mas (el mismo colectivo
-- pasa por varias paradas de la zona y no se cuenta varias veces). Paradas: cuantas tiene la linea en la zona.
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

-- Paradas de cada modo en la zona (sin importar la franja)
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
