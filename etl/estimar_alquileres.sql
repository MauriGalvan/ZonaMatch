-- Carga app.alquileres_zona con un alquiler ESTIMADO por zona base (barrios y localidades), para que el limite de
-- alquiler del analisis funcione hasta que llegue el scraping de Argenprop. Lo corre DATOS-ZONAS-DOCKER.bat
-- despues de calcular_zonas.sql; tambien se puede correr solo:
--   docker compose exec -T db psql -U zonamatch -d zonamatch_docker < etl\estimar_alquileres.sql
--
-- NO son precios reales. Se parte de un alquiler base de CABA (:base, un 2 ambientes promedio) y se ajusta por
-- el nivel socioeconomico de la zona segun el censo 2022 (proporcion de personas con estudios universitarios);
-- fuera de CABA se descuenta un 25 %. Las propiedades disponibles son el 3 % de los hogares que alquilan.
-- Solo completa zonas sin dato: no pisa lo que ya cargo otra fuente (fuente <> 'estimado').
--
-- Cambiar la base: psql -v base=900000 ...

\set ON_ERROR_STOP on
\if :{?base}
\else
    \set base 750000
\endif

-- La tabla la crea la migracion AgregarAlquileresZona del backend: sin ella no hay nada que cargar
SELECT to_regclass('app.alquileres_zona') IS NOT NULL AS existe \gset
\if :existe
BEGIN;

WITH caba AS (
    SELECT geom FROM geo.zonas WHERE nivel = 4 ORDER BY superficie_km2 DESC LIMIT 1
),
base AS (
    SELECT z.slug,
           i.hogares_alquilan,
           i.personas_universitario::numeric / nullif(i.personas_con_dato_educacion, 0) AS universitarios,
           EXISTS (SELECT 1 FROM caba c WHERE ST_Contains(c.geom, ST_PointOnSurface(z.geom))) AS en_caba
    FROM geo.zonas z
    JOIN geo.zona_indicadores i USING (osm_id)
    WHERE z.elegida AND z.nivel > 5 AND i.hogares > 0
)
INSERT INTO app.alquileres_zona (zona_slug, alquiler_promedio, propiedades_disponibles, fuente, fecha_actualizacion)
SELECT DISTINCT ON (slug)
       slug,
       -- redondeado a miles de pesos
       round(:base * (0.6 + 2.5 * coalesce(universitarios, 0.07)) * CASE WHEN en_caba THEN 1 ELSE 0.75 END, -3),
       round(hogares_alquilan * 0.03)::int,
       'estimado',
       now()
FROM base
ORDER BY slug
ON CONFLICT (zona_slug) DO UPDATE
    SET alquiler_promedio = excluded.alquiler_promedio,
        propiedades_disponibles = excluded.propiedades_disponibles,
        fecha_actualizacion = excluded.fecha_actualizacion
    WHERE app.alquileres_zona.fuente = 'estimado';

COMMIT;
\else
\echo 'Falta app.alquileres_zona: aplicar las migraciones del backend (dotnet ef database update) y volver a correr este script.'
\endif
