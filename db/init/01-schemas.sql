-- Runs once, when the db_data volume is created for the first time (docker-entrypoint-initdb.d).
--   app : application model, managed by EF Core migrations
--   geo : external GIS data (partidos, comunas, radios, escuelas, transport...), loaded from the dump / ETL
--   osm : raw osm2pgsql import (OSM-DATA-UPDATER-DOCKER.bat), dropped and recreated on every import
-- PostGIS itself stays in "public" (installed by the postgis image), so keep "public" in the search_path.
CREATE SCHEMA IF NOT EXISTS app;
CREATE SCHEMA IF NOT EXISTS geo;
CREATE SCHEMA IF NOT EXISTS osm;
