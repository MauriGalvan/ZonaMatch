-- Census fractions of the AMBA with what etl/crime/fit_crime_model.py uses to spread the crimes of a partido over
-- its localidades: census indicators, density, shops (OSM) and public transport departures (GTFS), plus the crimes
-- of the Mapa del Delito for the CABA fractions, where the model is fitted.
-- Run by DATOS-ZONAS-DOCKER.bat after calcular_zonas.sql and calcular_transporte.sql:
--   psql --csv < etl\crime\fraction_features.sql > datos\delitos_fracciones_entrada.csv
WITH fr AS (
    -- comuna: the CABA comuna (the census numbers them by 7); null in the province
    SELECT r.prov || r.depto || r.fracc AS fid, r.prov,
           CASE WHEN r.prov = '02' THEN r.depto::int / 7 END AS comuna,
           sum(r.poblacion) AS pob, ST_Union(r.geom) AS geom
    FROM geo.censo_radios r
    GROUP BY 1, 2, 3
),
ind AS (
    SELECT r.prov || r.depto || r.fracc AS fid,
           sum(i.conteo) FILTER (WHERE i.variable = 'HOGAR_H24A') AS hogares,
           sum(i.conteo) FILTER (WHERE i.variable = 'HOGAR_NBI_TOT' AND i.categoria = '1') AS nbi,
           sum(i.conteo) FILTER (WHERE i.variable = 'HOGAR_H20CP' AND i.categoria IN ('5', '6')) AS hac,
           sum(i.conteo) FILTER (WHERE i.variable = 'HOGAR_H22' AND i.categoria = '2') AS alq,
           sum(i.conteo) FILTER (WHERE i.variable = 'PERSONA_MNI' AND i.categoria IN ('9', '10', '11')) AS univ,
           sum(i.conteo) FILTER (WHERE i.variable = 'PERSONA_MNI' AND i.categoria <> '99') AS edu,
           sum(i.conteo) FILTER (WHERE i.variable = 'PERSONA_P19' AND i.categoria IN ('1', '2')) AS cob,
           sum(i.conteo) FILTER (WHERE i.variable = 'PERSONA_P19') AS cobt
    FROM geo.censo_radios r JOIN geo.censo_indicadores i USING (cod)
    GROUP BY 1
),
del AS (
    SELECT f.fid,
           coalesce(sum(d.cantidad) FILTER (WHERE d.tipo <> 'Vialidad'), 0) AS total,
           coalesce(sum(d.cantidad) FILTER (WHERE d.tipo = 'Robo'), 0) AS robos
    FROM fr f LEFT JOIN geo.delitos_caba d ON f.prov = '02' AND ST_Contains(f.geom, d.geom)
    GROUP BY f.fid
),
tiendas AS (
    SELECT f.fid, count(p.*) AS comercios
    -- Shops and the amenities that bring people (bars, banks...), counted as in the zone detail
    FROM fr f LEFT JOIN osm.planet_osm_point p
      ON (p.shop IS NOT NULL OR p.amenity IN ('restaurant', 'bar', 'cafe', 'fast_food', 'bank', 'pharmacy'))
     AND ST_Contains(f.geom, ST_Transform(p.way, 4326))
    GROUP BY f.fid
),
transporte AS (
    SELECT f.fid, coalesce(sum(t.salidas), 0) AS salidas
    FROM fr f LEFT JOIN geo.transporte_salidas t ON ST_Contains(f.geom, t.geom)
    GROUP BY f.fid
)
SELECT f.fid, f.prov, f.comuna, f.pob, ST_Area(f.geom::geography) / 1e6 AS km2,
       i.hogares, i.nbi, i.hac, i.alq, i.univ, i.edu, i.cob, i.cobt,
       t.comercios, s.salidas, d.total, d.robos
FROM fr f
JOIN ind i USING (fid) JOIN del d USING (fid) JOIN tiendas t USING (fid) JOIN transporte s USING (fid)
WHERE f.pob > 0
