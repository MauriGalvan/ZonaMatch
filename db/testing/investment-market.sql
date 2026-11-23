\set ON_ERROR_STOP on

BEGIN;

DO $$
BEGIN
    IF current_database() <> 'zonamatch_e2e' THEN
        RAISE EXCEPTION 'The investment fixture requires the zonamatch_e2e database.';
    END IF;
    IF NOT EXISTS (SELECT FROM geo.zonas WHERE osm_id = -2 AND slug = 'zona-vecina-e2e') THEN
        RAISE EXCEPTION 'Load analysis-zones.sql before the investment fixture.';
    END IF;
END;
$$;

-- The two zones have their own sale samples and distinct rental yields.
INSERT INTO app.zone_market (
    zone_slug, operation, property_type, currency, listings, sample_size,
    average_price, median_price, source, updated_at
)
VALUES
    ('zona-e2e', 'sale', 'apartment', 'USD', 16, 16,  95000,  95000, 'e2e', CURRENT_TIMESTAMP),
    ('zona-e2e', 'rent', 'apartment', 'ARS', 12, 12, 780000, 780000, 'e2e', CURRENT_TIMESTAMP),
    ('zona-vecina-e2e', 'sale', 'apartment', 'USD', 12, 12, 125000, 125000, 'e2e', CURRENT_TIMESTAMP),
    ('zona-vecina-e2e', 'rent', 'apartment', 'ARS', 10, 10, 720000, 720000, 'e2e', CURRENT_TIMESTAMP)
ON CONFLICT (zone_slug, operation, property_type) DO NOTHING;

COMMIT;
