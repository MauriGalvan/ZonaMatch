-- Final table of the Zonaprop market per zone, as CSV (MERCADO-ZONAS-DOCKER.bat writes it to
-- datos\zonaprop\market_by_zone.csv). See calculate_market.sql for the meaning of each column.
SELECT m.zone_slug, z.nombre AS zone_name, z.nivel AS level, m.operation, m.property_type, m.listings,
       m.sample_size, m.avg_rent_ars, m.median_rent_ars, m.avg_expenses_ars, m.avg_price_usd, m.avg_usd_m2,
       m.median_usd_m2, m.source, m.inherited_from, m.scraped_at::date AS scraped_on
FROM geo.zone_market m
JOIN geo.zonas z USING (osm_id)
ORDER BY z.nivel, m.zone_slug, m.operation DESC,
         array_position(ARRAY['todos', 'departamentos', 'casas', 'ph'], m.property_type)
