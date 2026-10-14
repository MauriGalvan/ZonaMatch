-- Runs once, when the db_data volume is created for the first time (docker-entrypoint-initdb.d).
--   app : application model, managed by EF Core migrations
--   geo : external GIS data (partidos, comunas, radios, escuelas, transport...), loaded from the dump / ETL
--   osm : raw osm2pgsql import (OSM-DATA-UPDATER-DOCKER.bat), dropped and recreated on every import
-- PostGIS itself stays in "public", so keep "public" in the search_path.
-- Mounting ./db/init over docker-entrypoint-initdb.d hides the postgis image's own init script, so the
-- extension has to be created here (osm2pgsql and the geo dump fail without it).
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE SCHEMA IF NOT EXISTS app;
CREATE SCHEMA IF NOT EXISTS geo;
CREATE SCHEMA IF NOT EXISTS osm;
