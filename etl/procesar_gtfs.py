"""
Resume los GTFS de transporte publico del AMBA en salidas por parada, linea y franja horaria de un dia habil.
No se carga el GTFS completo en la base: solo este resumen (datos/transporte_salidas.csv), que despues
etl/calcular_transporte.sql asigna a cada zona.

Feeds (Buenos Aires Data, descarga publica): colectivos (2019), trenes (2020) y subte. Son los ultimos
publicados sin credenciales; los horarios pueden no estar al dia.

Uso:  python etl/procesar_gtfs.py            (desde backend/ZonaMatch)
Requiere: pip install duckdb
"""

import sys
import time
import urllib.request
import zipfile
from pathlib import Path

import duckdb

CARPETA_DATOS = Path(__file__).resolve().parent.parent / "datos"
CARPETA_GTFS = CARPETA_DATOS / "gtfs"

CDN = "https://cdn.buenosaires.gob.ar/datosabiertos/datasets/"
AGENTE = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ZonaMatch-ETL/1.0"

# modo: (archivo, url, service_id de lunes a viernes)
FEEDS = {
    "colectivo": ("colectivos-gtfs.zip", CDN + "transporte-y-obras-publicas/colectivos-gtfs/colectivos-gtfs.zip", "3"),
    "tren": ("trenes-gtfs.zip", CDN + "trenes-gtfs/trenes-gtfs.zip", "1"),
    "subte": ("subte-gtfs-zip.zip", CDN + "sbase/subte-gtfs/subte-gtfs-zip.zip", "5"),
}

# Franjas horarias (hora de inicio inclusive, hora de fin exclusive)
FRANJAS = [
    ("madrugada", 0, 6),
    ("manana", 6, 9),
    ("mediodia", 9, 16),
    ("tarde", 16, 20),
    ("noche", 20, 24),
]


def descargar(archivo: str, url: str) -> Path:
    destino = CARPETA_DATOS / archivo
    if destino.exists():
        return destino
    print(f"  descargando {archivo}...")
    pedido = urllib.request.Request(url, headers={"User-Agent": AGENTE})
    with urllib.request.urlopen(pedido, timeout=900) as respuesta, open(destino, "wb") as salida:
        while bloque := respuesta.read(1 << 20):
            salida.write(bloque)
    return destino


def extraer(modo: str, zip_path: Path) -> Path:
    """Descomprime el feed en datos/gtfs/<modo>/. El de subte trae otro zip adentro."""
    carpeta = CARPETA_GTFS / modo
    if (carpeta / "stop_times.txt").exists():
        return carpeta
    carpeta.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(zip_path) as archivo:
        archivo.extractall(carpeta)
    for interno in list(carpeta.iterdir()):
        if interno.is_file() and zipfile.is_zipfile(interno):
            with zipfile.ZipFile(interno) as anidado:
                anidado.extractall(carpeta)
            interno.unlink()
    return carpeta


def leer(conexion: duckdb.DuckDBPyConnection, carpeta: Path, nombre: str, tabla: str) -> bool:
    archivo = carpeta / nombre
    if not archivo.exists():
        return False
    conexion.sql(f"CREATE OR REPLACE TABLE {tabla} AS SELECT * FROM read_csv('{archivo.as_posix()}', header=true, all_varchar=true)")
    return True


def franja_sql(hora: str) -> str:
    casos = " ".join(f"WHEN {hora} >= {inicio} AND {hora} < {fin} THEN '{nombre}'" for nombre, inicio, fin in FRANJAS)
    # Los GTFS usan horas mayores a 24 para los viajes que siguen despues de medianoche
    return f"CASE {casos.replace(hora, f'({hora} % 24)')} END"


def salidas_por_parada(conexion: duckdb.DuckDBPyConnection, modo: str, carpeta: Path, servicio: str) -> None:
    for nombre in ["stops", "routes", "trips", "stop_times"]:
        leer(conexion, carpeta, f"{nombre}.txt", nombre)
    con_frecuencias = leer(conexion, carpeta, "frequencies.txt", "frequencies")

    # Hora de salida en segundos desde la medianoche del dia de servicio
    conexion.sql("""
        CREATE OR REPLACE TABLE paradas_viaje AS
        SELECT st.trip_id, st.stop_id, t.route_id,
               split_part(st.departure_time, ':', 1)::int * 3600 + split_part(st.departure_time, ':', 2)::int * 60
                 + split_part(st.departure_time, ':', 3)::int AS segundos,
               st.stop_sequence::int AS orden
        FROM stop_times st
        JOIN trips t USING (trip_id)
        WHERE t.service_id = $servicio AND st.departure_time IS NOT NULL AND st.departure_time <> ''
    """, params={"servicio": servicio})

    if con_frecuencias:
        # Viajes por frecuencia (subte): cada viaje de stop_times es un modelo que se repite cada headway_secs
        # entre start_time y end_time; la salida en cada parada es la del modelo corrida por el desfase
        conexion.sql("""
            CREATE OR REPLACE TABLE salidas AS
            WITH modelo AS (
                SELECT p.*, p.segundos - min(p.segundos) OVER (PARTITION BY p.trip_id) AS desfase
                FROM paradas_viaje p
            ),
            ventanas AS (
                SELECT trip_id,
                       split_part(start_time, ':', 1)::int * 3600 + split_part(start_time, ':', 2)::int * 60 AS inicio,
                       split_part(end_time, ':', 1)::int * 3600 + split_part(end_time, ':', 2)::int * 60 AS fin,
                       headway_secs::int AS cada
                FROM frequencies
            )
            SELECT m.stop_id, m.route_id, v.inicio + s.n * v.cada + m.desfase AS segundos
            FROM modelo m
            JOIN ventanas v USING (trip_id)
            CROSS JOIN LATERAL (SELECT unnest(range(0, ((v.fin - v.inicio) / v.cada)::int + 1)) AS n) s
        """)
    else:
        conexion.sql("CREATE OR REPLACE TABLE salidas AS SELECT stop_id, route_id, segundos FROM paradas_viaje")

    conexion.sql(f"""
        INSERT INTO resumen
        SELECT '{modo}' AS modo, s.stop_id AS parada_id, any_value(p.stop_name) AS parada,
               any_value(p.stop_lat)::double AS latitud, any_value(p.stop_lon)::double AS longitud,
               coalesce(nullif(any_value(r.route_short_name), ''), any_value(r.route_long_name)) AS linea,
               {franja_sql('(s.segundos // 3600)')} AS franja,
               count(*) AS salidas
        FROM salidas s
        JOIN stops p USING (stop_id)
        JOIN routes r USING (route_id)
        GROUP BY s.stop_id, s.route_id, franja
    """)


def main() -> int:
    CARPETA_DATOS.mkdir(exist_ok=True)
    inicio = time.time()
    conexion = duckdb.connect()
    conexion.sql("""
        CREATE TABLE resumen (modo text, parada_id text, parada text, latitud double, longitud double,
                              linea text, franja text, salidas integer)
    """)

    for modo, (archivo, url, servicio) in FEEDS.items():
        print(f"{modo}...")
        carpeta = extraer(modo, descargar(archivo, url))
        salidas_por_parada(conexion, modo, carpeta, servicio)

    destino = CARPETA_DATOS / "transporte_salidas.csv"
    conexion.sql(f"COPY (SELECT * FROM resumen WHERE franja IS NOT NULL) TO '{destino.as_posix()}' (HEADER)")
    print(conexion.sql("SELECT modo, count(DISTINCT parada_id) paradas, count(DISTINCT linea) lineas, sum(salidas) salidas FROM resumen GROUP BY 1"))
    print(f"Listo en {time.time() - inicio:.0f} s: {destino}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
