-- Zonaprop location of every zone, as the JSON object the SPA keeps in
-- src/features/zone-detail/data/zonaprop-slugs.json (zone slug -> Zonaprop slug), for the "Ver propiedades" link.
-- Run after calculate_market.sql, from backend/ZonaMatch:
--   docker compose exec -T db psql -U zonamatch -d zonamatch_docker -At < etl\zonaprop\export_front_slugs.sql > zonaprop-slugs.json
-- Rules: a zone measured with its own Zonaprop location uses it; a zone that inherits its prices uses the location of
-- the zone it inherits from; comunas (and the whole city) open CABA; partidos use their name ("partido-de-moron" ->
-- "moron"); any other zone uses the nearest zone that has a location.
WITH zones AS (
    SELECT DISTINCT ON (slug) slug, nombre, nivel, geom
    FROM geo.zonas
    WHERE elegida
    ORDER BY slug, nivel DESC
),
own AS (
    SELECT DISTINCT ON (t.zone_slug) t.zone_slug, t.zonaprop_slug
    FROM geo.load_zonaprop_totals t
    JOIN app.zone_market m ON m.zone_slug = t.zone_slug AND m.source = 'zonaprop'
    ORDER BY t.zone_slug
),
located AS (
    SELECT z.slug, z.geom, o.zonaprop_slug FROM zones z JOIN own o ON o.zone_slug = z.slug
),
resolved AS (
    SELECT z.slug,
           CASE
               WHEN z.nombre LIKE 'Comuna %' OR z.slug = 'buenos-aires' THEN 'capital-federal'
               WHEN o.zonaprop_slug IS NOT NULL THEN o.zonaprop_slug
               WHEN z.nivel = 5 THEN trim(BOTH '-' FROM regexp_replace(
                   translate(lower(regexp_replace(z.nombre, '^Partido de ', '', 'i')), 'áéíóúüñ', 'aeiouun'),
                   '[^a-z0-9]+', '-', 'g'))
               ELSE coalesce(inherited.zonaprop_slug, nearest.zonaprop_slug)
           END AS zonaprop_slug
    FROM zones z
    LEFT JOIN own o ON o.zone_slug = z.slug
    LEFT JOIN LATERAL (
        SELECT i.zonaprop_slug
        FROM app.zone_market m
        JOIN own i ON i.zone_slug = m.inherited_from
        WHERE m.zone_slug = z.slug AND m.property_type = 'all'
        LIMIT 1
    ) inherited ON true
    LEFT JOIN LATERAL (
        SELECT l.zonaprop_slug FROM located l ORDER BY l.geom <-> z.geom LIMIT 1
    ) nearest ON true
)
-- One line, sorted like the file in the SPA
SELECT '{' || string_agg(to_json(slug)::text || ':' || to_json(zonaprop_slug)::text, ',' ORDER BY slug COLLATE "C") || '}'
FROM resolved
WHERE zonaprop_slug IS NOT NULL;
