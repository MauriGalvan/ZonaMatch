-- sitemap.xml of the SPA (https://zonamatch.com.ar): public pages and the detail page of every chosen zone.
-- ZONE-DATA-DOCKER.bat writes it to datos\sitemap.xml and copies it to the SPA's public folder. Run with psql -At.
-- Slugs come from geo.zonas, which keeps them fixed across imports (geo.zona_slugs, etl/calculate_zones.sql).

SELECT '<?xml version="1.0" encoding="UTF-8"?>' || chr(10)
    || '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">' || chr(10)
    || string_agg('  <url><loc>https://zonamatch.com.ar' || path || '</loc></url>', chr(10) ORDER BY sort_order, path) || chr(10)
    || '</urlset>'
FROM (
    SELECT path, 0 AS sort_order
    FROM (VALUES ('/'), ('/zonas'), ('/terminos'), ('/privacidad')) AS public_pages (path)
    UNION ALL
    SELECT '/zonas/' || slug, 1
    FROM geo.zonas
    WHERE elegida
) urls;
