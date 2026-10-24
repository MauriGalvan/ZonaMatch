"""
Descarga los datos abiertos que completan el detalle de las zonas y los deja en datos/ como CSV.

- Censo 2022 (INDEC, resultados definitivos por radio censal), publicado como Parquet por el proyecto
  censoargentino a partir de la base REDATAM de INDEC. Solo se leen los radios del AMBA.
- Delitos de CABA (Buenos Aires Data, Mapa del Delito), con ubicacion de cada hecho.

Uso:  python etl/descargar_datos.py            (desde backend/ZonaMatch)
Requiere: pip install duckdb
"""

import sys
import time
import urllib.request
from pathlib import Path

import duckdb

CARPETA_DATOS = Path(__file__).resolve().parent.parent / "datos"

CENSO_BASE = "https://huggingface.co/datasets/pedroorden/censoargentino/resolve/main/"

# Recorte del AMBA: el mismo del mapa de OSM (OSM-DATA-UPDATER-DOCKER.bat), con margen
RECORTE_AMBA = {"xmin": -59.20, "xmax": -57.90, "ymin": -35.20, "ymax": -34.20}

# Variables del censo que usa el detalle de zona (ver etl/cargar_datos.sql)
VARIABLES_CENSO = [
    "PERSONA_EDADGRU",  # edad en grandes grupos
    "PERSONA_P19",      # cobertura de salud
    "PERSONA_MNI",      # maximo nivel de instruccion
    "HOGAR_H24A",       # internet en la vivienda
    "HOGAR_H24B",       # celular con internet
    "HOGAR_H24C",       # computadora o tablet
    "HOGAR_NBI_TOT",    # necesidades basicas insatisfechas
    "HOGAR_H20CP",      # personas por cuarto (hacinamiento)
    "HOGAR_H22",        # propiedad de la vivienda (alquila o no)
]

DELITOS_ANIO = 2024
DELITOS_URL = (
    "https://cdn.buenosaires.gob.ar/datosabiertos/datasets/ministerio-de-justicia-y-seguridad/"
    f"delitos/delitos_{DELITOS_ANIO}.csv"
)

# El portal rechaza pedidos sin un navegador conocido
AGENTE = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ZonaMatch-ETL/1.0"


def descargar_censo(conexion: duckdb.DuckDBPyConnection) -> None:
    radios = CARPETA_DATOS / "censo_radios_amba.csv"
    indicadores = CARPETA_DATOS / "censo_indicadores_amba.csv"

    print("Censo 2022: radios del AMBA con poblacion y geometria...")
    conexion.sql(f"""
        CREATE OR REPLACE TABLE radios AS
        SELECT COD_2022 AS cod, PROV AS prov, DEPTO AS depto, FRACC AS fracc, RADIO AS radio,
               POB_TOT_P AS poblacion, VIV_TOT_P AS viviendas, ST_AsText(geometry) AS wkt
        FROM '{CENSO_BASE}radios-2022.parquet'
        WHERE PROV IN ('02', '06')
          AND bbox.xmin > {RECORTE_AMBA['xmin']} AND bbox.xmax < {RECORTE_AMBA['xmax']}
          AND bbox.ymin > {RECORTE_AMBA['ymin']} AND bbox.ymax < {RECORTE_AMBA['ymax']}
    """)
    conexion.sql(f"COPY radios TO '{radios.as_posix()}' (HEADER)")

    print("Censo 2022: indicadores de esos radios...")
    lista = ", ".join(f"'{variable}'" for variable in VARIABLES_CENSO)
    conexion.sql(f"""
        CREATE OR REPLACE TABLE indicadores AS
        SELECT l.id_geo AS cod, l.codigo_variable AS variable, l.valor_categoria AS categoria, l.conteo
        FROM '{CENSO_BASE}censo-2022-largo.parquet' l
        JOIN radios r ON r.cod = l.id_geo
        WHERE l.valor_provincia IN ('02', '06') AND l.codigo_variable IN ({lista})
    """)
    conexion.sql(f"COPY indicadores TO '{indicadores.as_posix()}' (HEADER)")

    cantidad = conexion.sql("SELECT count(*), sum(poblacion) FROM radios").fetchall()[0]
    print(f"  {cantidad[0]} radios, {cantidad[1]} habitantes")


def descargar_delitos() -> None:
    destino = CARPETA_DATOS / f"delitos_{DELITOS_ANIO}.csv"
    print(f"Delitos de CABA {DELITOS_ANIO} (Buenos Aires Data)...")
    pedido = urllib.request.Request(DELITOS_URL, headers={"User-Agent": AGENTE})
    with urllib.request.urlopen(pedido, timeout=300) as respuesta, open(destino, "wb") as archivo:
        archivo.write(respuesta.read())
    print(f"  {destino.stat().st_size / 1_000_000:.1f} MB")


def main() -> int:
    CARPETA_DATOS.mkdir(exist_ok=True)
    inicio = time.time()

    conexion = duckdb.connect()
    conexion.sql("INSTALL httpfs; LOAD httpfs; INSTALL spatial; LOAD spatial;")

    descargar_censo(conexion)
    descargar_delitos()

    print(f"Listo en {time.time() - inicio:.0f} s. Datos en {CARPETA_DATOS}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
