"""
Downloads open data used to enrich zone details and saves it as CSV files in datos/.

- 2022 Census (INDEC, final results by census tract), published as Parquet by the censoargentino project
  from INDEC's REDATAM database. Only AMBA tracts are read.
- CABA crime data (Buenos Aires Data, Mapa del Delito), including the location of each incident.

Usage:     python etl/download_open_data.py    (from backend/ZonaMatch)
Requires:  pip install duckdb
"""

import sys
import time
import urllib.request
from pathlib import Path

import duckdb

DATA_DIR = Path(__file__).resolve().parent.parent / "datos"

CENSUS_BASE_URL = "https://huggingface.co/datasets/pedroorden/censoargentino/resolve/main/"

# AMBA bounds: the same area used by the OSM map updater, with an additional margin.
AMBA_BOUNDS = {"xmin": -59.20, "xmax": -57.90, "ymin": -35.20, "ymax": -34.20}

# Census variables used by zone details (see etl/create_geographic_tables.sql).
CENSUS_VARIABLES = [
    "PERSONA_EDADGRU",  # Age groups.
    "PERSONA_EDADQUI",  # Quinquennial age groups.
    "PERSONA_EDAD",     # Exact age.
    "HOGAR_TOTPERS",    # Persons per household.
    "PERSONA_P19",      # Health coverage.
    "PERSONA_MNI",      # Highest education level.
    "HOGAR_H24A",       # Internet access at home.
    "HOGAR_H24B",       # Mobile phone with internet access.
    "HOGAR_H24C",       # Computer or tablet.
    "HOGAR_NBI_TOT",    # Unsatisfied basic needs.
    "HOGAR_H20CP",      # People per room (overcrowding).
    "HOGAR_H22",        # Home ownership status (rented or not).
]

CRIME_YEAR = 2024
CRIME_DATA_URL = (
    "https://cdn.buenosaires.gob.ar/datosabiertos/datasets/ministerio-de-justicia-y-seguridad/"
    f"delitos/delitos_{CRIME_YEAR}.csv"
)

# The portal rejects requests without a recognized browser user agent.
USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ZonaMatch-ETL/1.0"


def download_census(connection: duckdb.DuckDBPyConnection) -> None:
    census_tracts_file = DATA_DIR / "censo_radios_amba.csv"
    indicators_file = DATA_DIR / "censo_indicadores_amba.csv"

    print("Census 2022: AMBA tracts with population and geometry...")
    connection.sql(f"""
        CREATE OR REPLACE TABLE census_tracts AS
        SELECT COD_2022 AS cod, PROV AS prov, DEPTO AS depto, FRACC AS fracc, RADIO AS radio,
               POB_TOT_P AS poblacion, VIV_TOT_P AS viviendas, ST_AsText(geometry) AS wkt
        FROM '{CENSUS_BASE_URL}radios-2022.parquet'
        WHERE PROV IN ('02', '06')
          AND bbox.xmin > {AMBA_BOUNDS['xmin']} AND bbox.xmax < {AMBA_BOUNDS['xmax']}
          AND bbox.ymin > {AMBA_BOUNDS['ymin']} AND bbox.ymax < {AMBA_BOUNDS['ymax']}
    """)
    connection.sql(f"COPY census_tracts TO '{census_tracts_file.as_posix()}' (HEADER)")

    print("Census 2022: indicators for those tracts...")
    variable_list = ", ".join(f"'{variable}'" for variable in CENSUS_VARIABLES)
    connection.sql(f"""
        CREATE OR REPLACE TABLE indicators AS
        SELECT l.id_geo AS cod, l.codigo_variable AS variable, l.valor_categoria AS categoria, l.conteo
        FROM '{CENSUS_BASE_URL}censo-2022-largo.parquet' l
        JOIN census_tracts r ON r.cod = l.id_geo
        WHERE l.valor_provincia IN ('02', '06') AND l.codigo_variable IN ({variable_list})
    """)
    connection.sql(f"COPY indicators TO '{indicators_file.as_posix()}' (HEADER)")

    tract_summary = connection.sql("SELECT count(*), sum(poblacion) FROM census_tracts").fetchall()[0]
    print(f"  {tract_summary[0]} tracts, {tract_summary[1]} residents")


def download_crime_data() -> None:
    destination = DATA_DIR / f"delitos_{CRIME_YEAR}.csv"
    print(f"CABA crime data {CRIME_YEAR} (Buenos Aires Data)...")
    request = urllib.request.Request(CRIME_DATA_URL, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=300) as response, open(destination, "wb") as output_file:
        output_file.write(response.read())
    print(f"  {destination.stat().st_size / 1_000_000:.1f} MB")


def main() -> int:
    DATA_DIR.mkdir(exist_ok=True)
    started_at = time.time()

    connection = duckdb.connect()
    connection.sql("INSTALL httpfs; LOAD httpfs; INSTALL spatial; LOAD spatial;")

    download_census(connection)
    download_crime_data()

    print(f"Completed in {time.time() - started_at:.0f} s. Data saved in {DATA_DIR}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
