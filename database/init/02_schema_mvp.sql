-- 02_schema_mvp.sql
-- Esquema relacional y geoespacial del MVP de ZonaMatch

-- -------------------------------------------------------------
-- 1. ZONAS Y UBICACIONES NORMALIZADAS (Georef)
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS locations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    georef_id VARCHAR(50),
    normalized_name VARCHAR(255) NOT NULL,
    department VARCHAR(100),
    province VARCHAR(100),
    latitude NUMERIC(10, 7) NOT NULL,
    longitude NUMERIC(10, 7) NOT NULL,
    geom GEOMETRY(Point, 4326) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_locations_geom ON locations USING GIST(geom);
CREATE INDEX IF NOT EXISTS idx_locations_georef_id ON locations(georef_id);
CREATE INDEX IF NOT EXISTS idx_locations_name_trgm ON locations USING gin (normalized_name gin_trgm_ops);

-- -------------------------------------------------------------
-- 2. PUNTOS DE INTERÉS URBANOS (Overpass / OpenStreetMap)
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS pois (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    osm_id BIGINT UNIQUE,
    name VARCHAR(255),
    category VARCHAR(50) NOT NULL,       -- 'salud', 'educacion', 'abastecimiento', 'espacios_verdes', 'transporte'
    subcategory VARCHAR(50) NOT NULL,    -- 'hospital', 'farmacia', 'escuela', 'supermercado', 'parque', 'parada_colectivo'
    latitude NUMERIC(10, 7) NOT NULL,
    longitude NUMERIC(10, 7) NOT NULL,
    geom GEOMETRY(Point, 4326) NOT NULL,
    tags JSONB,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_pois_geom ON pois USING GIST(geom);
CREATE INDEX IF NOT EXISTS idx_pois_category ON pois(category);
CREATE INDEX IF NOT EXISTS idx_pois_subcategory ON pois(subcategory);
CREATE INDEX IF NOT EXISTS idx_pois_category_geom ON pois USING GIST(geom) INCLUDE (category, subcategory);

-- -------------------------------------------------------------
-- 3. CACHÉ METEOROLÓGICO (Open-Meteo)
-- Cuadrícula redondeada a 0.05 grados (~5km) para máxima tasa de acierto en caché
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS weather_cache (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    grid_lat NUMERIC(6, 2) NOT NULL,
    grid_lon NUMERIC(6, 2) NOT NULL,
    geom GEOMETRY(Point, 4326) NOT NULL,
    avg_temperature NUMERIC(4, 1),
    max_temperature NUMERIC(4, 1),
    min_temperature NUMERIC(4, 1),
    annual_precipitation_mm NUMERIC(6, 1),
    rainy_days_count INT,
    raw_data JSONB,
    fetched_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    expires_at TIMESTAMP WITH TIME ZONE DEFAULT (NOW() + INTERVAL '30 days'),
    CONSTRAINT uq_weather_grid UNIQUE (grid_lat, grid_lon)
);

CREATE INDEX IF NOT EXISTS idx_weather_cache_geom ON weather_cache USING GIST(geom);

-- -------------------------------------------------------------
-- 4. PERFILES DE USUARIO Y PONDERACIONES
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS user_profiles (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    email VARCHAR(255) UNIQUE,
    name VARCHAR(100),
    household_type VARCHAR(50) DEFAULT 'individual', -- individual, pareja, familia_con_hijos
    max_travel_time_minutes INT DEFAULT 45,
    -- Ponderaciones: 0 = ignorar, 1 = baja, 2 = media, 3 = alta
    weights JSONB NOT NULL DEFAULT '{
        "salud": 2,
        "educacion": 2,
        "abastecimiento": 2,
        "espacios_verdes": 2,
        "movilidad": 3,
        "clima": 1
    }'::jsonb,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- -------------------------------------------------------------
-- 5. DESTINOS FRECUENTES DEL USUARIO (Para OSRM)
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS user_destinations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    profile_id UUID NOT NULL REFERENCES user_profiles(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,          -- 'Trabajo', 'Facultad', 'Colegio Niños'
    address VARCHAR(255),
    latitude NUMERIC(10, 7) NOT NULL,
    longitude NUMERIC(10, 7) NOT NULL,
    geom GEOMETRY(Point, 4326) NOT NULL,
    transport_mode VARCHAR(20) DEFAULT 'driving', -- 'driving', 'walking', 'cycling'
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_user_destinations_geom ON user_destinations USING GIST(geom);
CREATE INDEX IF NOT EXISTS idx_user_destinations_profile ON user_destinations(profile_id);

-- -------------------------------------------------------------
-- 6. EVALUACIONES Y RESULTADOS DE ZONAMATCH SCORE
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS zone_evaluations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    profile_id UUID REFERENCES user_profiles(id) ON DELETE SET NULL,
    latitude NUMERIC(10, 7) NOT NULL,
    longitude NUMERIC(10, 7) NOT NULL,
    evaluated_point GEOMETRY(Point, 4326) NOT NULL,
    address_query VARCHAR(255),
    total_score NUMERIC(5, 2) NOT NULL,        -- Escala 0.00 a 100.00
    data_confidence NUMERIC(5, 2) NOT NULL,    -- Escala 0.00 a 100.00 (% cobertura)
    subscores JSONB NOT NULL,                  -- { "salud": 85, "movilidad": 90, ... }
    mobility_details JSONB,                    -- Resultados de OSRM (tiempos y distancias)
    pros JSONB,                                -- ["Hospital a menos de 400m", "15 min al trabajo"]
    cons JSONB,                                -- ["Pocos espacios verdes a menos de 500m"]
    explanation TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_evaluations_point ON zone_evaluations USING GIST(evaluated_point);
CREATE INDEX IF NOT EXISTS idx_evaluations_profile ON zone_evaluations(profile_id);

-- -------------------------------------------------------------
-- 7. FUNCIONES AUXILIARES POSTGIS
-- -------------------------------------------------------------

-- Función para contar POIs por categoría dentro de un radio en metros
CREATE OR REPLACE FUNCTION fn_count_pois_in_radius(
    p_lat NUMERIC,
    p_lon NUMERIC,
    p_radius_meters DOUBLE PRECISION,
    p_category VARCHAR DEFAULT NULL
)
RETURNS TABLE (
    category VARCHAR,
    subcategory VARCHAR,
    total_count BIGINT
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        p.category,
        p.subcategory,
        COUNT(*)::BIGINT as total_count
    FROM pois p
    WHERE ST_DWithin(
        p.geom::geography,
        ST_SetSRID(ST_MakePoint(p_lon, p_lat), 4326)::geography,
        p_radius_meters
    )
    AND (p_category IS NULL OR p.category = p_category)
    GROUP BY p.category, p.subcategory;
END;
$$ LANGUAGE plpgsql;

-- Función para obtener la distancia mínima en metros al POI más cercano de una subcategoría
CREATE OR REPLACE FUNCTION fn_nearest_poi_distance(
    p_lat NUMERIC,
    p_lon NUMERIC,
    p_subcategory VARCHAR
)
RETURNS DOUBLE PRECISION AS $$
DECLARE
    min_dist DOUBLE PRECISION;
BEGIN
    SELECT ST_Distance(
        p.geom::geography,
        ST_SetSRID(ST_MakePoint(p_lon, p_lat), 4326)::geography
    )
    INTO min_dist
    FROM pois p
    WHERE p.subcategory = p_subcategory
    ORDER BY p.geom <-> ST_SetSRID(ST_MakePoint(p_lon, p_lat), 4326)
    LIMIT 1;

    RETURN COALESCE(min_dist, -1);
END;
$$ LANGUAGE plpgsql;
