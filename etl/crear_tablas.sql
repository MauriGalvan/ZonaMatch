-- Tables for open data used to enrich zone details (geo schema).
-- DATOS-ZONAS-DOCKER.bat creates them before copying CSV files from datos/ (etl/descargar_datos.py).
-- These external data tables are dropped and recreated on each load; they are not part of the application model.

\set ON_ERROR_STOP on
SET client_min_messages = warning;

-- Staging tables receive CSV data as provided by each source.
DROP TABLE IF EXISTS geo.carga_censo_radios, geo.carga_censo_indicadores, geo.carga_delitos;

CREATE TABLE geo.carga_censo_radios (
    cod        text,
    prov       text,
    depto      text,
    fracc      text,
    radio      text,
    poblacion  integer,
    viviendas  integer,
    wkt        text
);

CREATE TABLE geo.carga_censo_indicadores (
    cod        text,
    variable   text,
    categoria  text,
    conteo     integer
);

CREATE TABLE geo.carga_delitos (
    id_mapa    bigint,
    anio       integer,
    mes        text,
    dia        text,
    fecha      date,
    franja     integer,
    tipo       text,
    subtipo    text,
    uso_arma   text,
    uso_moto   text,
    barrio     text,
    comuna     integer,
    latitud    double precision,
    longitud   double precision,
    cantidad   integer
);

DROP TABLE IF EXISTS geo.carga_snic;

-- SNIC crime data by district and CABA commune (etl/download_snic.py): latest year, grouped by crime type.
CREATE TABLE geo.carga_snic (
    year            integer,
    department_id   text,
    department_name text,
    crime_group     text,
    events          integer,
    rate_per_100k   double precision
);

DROP TABLE IF EXISTS geo.carga_delitos_fracciones;

-- Factor for each census fraction to distribute a district's crimes (etl/crime/fit_crime_model.py).
CREATE TABLE geo.carga_delitos_fracciones (
    fraction_id     text,
    factor          double precision,
    homicide_factor double precision
);

DROP TABLE IF EXISTS geo.carga_transporte_salidas;

-- GTFS summary (etl/procesar_gtfs.py): weekday departures by stop, route, and time window.
CREATE TABLE geo.carga_transporte_salidas (
    modo       text,
    parada_id  text,
    parada     text,
    latitud    double precision,
    longitud   double precision,
    linea      text,
    franja     text,
    salidas    integer
);
