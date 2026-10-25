-- Tablas de los datos abiertos que completan el detalle de las zonas (schema geo).
-- Las crea DATOS-ZONAS-DOCKER.bat antes de copiar los CSV de datos/ (etl/descargar_datos.py).
-- Se borran y se vuelven a crear en cada carga: son datos externos, no del modelo de la aplicacion.

\set ON_ERROR_STOP on
SET client_min_messages = warning;

-- Tablas de carga: reciben los CSV tal cual vienen
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

-- Delitos por partido y comuna del SNIC (etl/download_snic.py): ultimo anio, por grupo de delito
CREATE TABLE geo.carga_snic (
    year            integer,
    department_id   text,
    department_name text,
    crime_group     text,
    events          integer,
    rate_per_100k   double precision
);

DROP TABLE IF EXISTS geo.carga_delitos_fracciones;

-- Factor de cada fraccion censal para repartir los delitos de un partido (etl/crime/fit_crime_model.py)
CREATE TABLE geo.carga_delitos_fracciones (
    fraction_id text,
    factor      double precision
);

DROP TABLE IF EXISTS geo.carga_transporte_salidas;

-- Resumen de los GTFS (etl/procesar_gtfs.py): salidas por parada, linea y franja de un dia habil
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
