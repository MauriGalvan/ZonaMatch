-- 01_init_postgis.sql
-- Habilitación de extensiones geoespaciales y utilitarias

CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- Verificación de la versión instalada de PostGIS
SELECT PostGIS_Full_Version();
