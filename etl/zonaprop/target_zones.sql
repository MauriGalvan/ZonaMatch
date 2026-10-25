-- Zones the Zonaprop scraper looks up: every chosen zone below partido/comuna level (CABA barrios, PBA localidades
-- and their barrios), with the partido or "CABA" that Zonaprop shows next to the location name.
-- CABA barrios go first so a partial run already covers the city.
-- PBA barrios inside a localidad are left out: Zonaprop rarely knows them and, when it does, the sample is tiny.
-- calculate_market.sql gives them the averages of their localidad (source 'zonaprop-inherited').
-- Used by MERCADO-ZONAS-DOCKER.bat: psql --csv < target_zones.sql > datos\zonaprop\zones.csv
SELECT z.slug,
       z.nombre AS name,
       z.nivel AS level,
       p.nombre AS parent_name,
       (p.nombre LIKE 'Comuna %') AS in_caba
FROM geo.zonas z
LEFT JOIN LATERAL (
    SELECT g.nombre
    FROM geo.zonas g
    WHERE g.nivel = 5 AND ST_Contains(g.geom, ST_PointOnSurface(z.geom))
    LIMIT 1
) p ON true
WHERE z.elegida AND z.nivel > 5
  AND NOT (coalesce(p.nombre LIKE 'Comuna %', false) = false AND z.nivel > 8 AND EXISTS (
      SELECT 1 FROM geo.zonas l
      WHERE l.elegida AND l.nivel = 8 AND l.osm_id <> z.osm_id AND ST_Contains(l.geom, ST_PointOnSurface(z.geom))))
ORDER BY (p.nombre LIKE 'Comuna %') DESC NULLS LAST, z.nivel, z.slug
