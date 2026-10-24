-- Real estate market per zone (app.zone_market) as CSV, with the zone name and level. MERCADO-ZONAS-DOCKER.bat writes
-- it to datos\zonaprop\market_by_zone.csv. See calculate_market.sql for the meaning of each column.
SELECT m.zone_slug, z.nombre AS zone_name, z.nivel AS level, m.operation, m.property_type, m.listings,
       m.sample_size, m.average_price, m.median_price, m.currency, m.average_price_m2, m.median_price_m2,
       m.average_expenses_ars, m.source, m.inherited_from, m.updated_at::date AS updated_on
FROM app.zone_market m
JOIN LATERAL (
    SELECT g.nombre, g.nivel FROM geo.zonas g WHERE g.slug = m.zone_slug AND g.elegida ORDER BY g.nivel DESC LIMIT 1
) z ON true
ORDER BY z.nivel, m.zone_slug, m.operation,
         array_position(ARRAY['all', 'apartment', 'house', 'ph'], m.property_type)
