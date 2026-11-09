"""
Summarizes AMBA public transit GTFS data into departures by stop, route, and time window for a weekday.
The full GTFS feed is not loaded into the database. Only this summary (datos/transporte_salidas.csv) is
created; etl/calculate_transit.sql later assigns it to each zone.
It also writes the weekday route shapes (datos/transporte_recorridos.csv, one WKT line per route and shape) for the
map layer; etl/calculate_transit_routes.sql loads them. Feeds without shapes (subway) are skipped.

Feeds (Buenos Aires Data, public downloads): buses (2019), trains (2020), and subway. These are the latest
publicly available feeds without credentials; schedules may be outdated.

Usage:     python etl/process_gtfs.py                 (from backend/ZonaMatch)
           python etl/process_gtfs.py --routes-only   only the route shapes
Requires:  pip install duckdb
"""

import sys
import time
import urllib.request
import zipfile
from pathlib import Path

import duckdb

DATA_DIR = Path(__file__).resolve().parent.parent / "datos"
GTFS_DIR = DATA_DIR / "gtfs"

CDN = "https://cdn.buenosaires.gob.ar/datosabiertos/datasets/"
USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ZonaMatch-ETL/1.0"

# Mode: (file name, URL, weekday service ID).
FEEDS = {
    "colectivo": ("colectivos-gtfs.zip", CDN + "transporte-y-obras-publicas/colectivos-gtfs/colectivos-gtfs.zip", "3"),
    "tren": ("trenes-gtfs.zip", CDN + "trenes-gtfs/trenes-gtfs.zip", "1"),
    "subte": ("subte-gtfs-zip.zip", CDN + "sbase/subte-gtfs/subte-gtfs-zip.zip", "5"),
}

# Time windows (inclusive start hour, exclusive end hour).
TIME_WINDOWS = [
    ("madrugada", 0, 6),
    ("manana", 6, 9),
    ("mediodia", 9, 16),
    ("tarde", 16, 20),
    ("noche", 20, 24),
]


def download(file_name: str, url: str) -> Path:
    destination = DATA_DIR / file_name
    if destination.exists():
        return destination
    print(f"  Downloading {file_name}...")
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=900) as response, open(destination, "wb") as output_file:
        while chunk := response.read(1 << 20):
            output_file.write(chunk)
    return destination


def extract_feed(mode: str, zip_path: Path) -> Path:
    """Extract a feed into datos/gtfs/<mode>/. The subway feed contains a nested ZIP archive."""
    directory = GTFS_DIR / mode
    if (directory / "stop_times.txt").exists():
        return directory
    directory.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(zip_path) as archive:
        archive.extractall(directory)
    for nested_file in list(directory.iterdir()):
        if nested_file.is_file() and zipfile.is_zipfile(nested_file):
            with zipfile.ZipFile(nested_file) as nested_archive:
                nested_archive.extractall(directory)
            nested_file.unlink()
    return directory


def load_csv(connection: duckdb.DuckDBPyConnection, directory: Path, file_name: str, table_name: str) -> bool:
    source_file = directory / file_name
    if not source_file.exists():
        return False
    connection.sql(f"CREATE OR REPLACE TABLE {table_name} AS SELECT * FROM read_csv('{source_file.as_posix()}', header=true, all_varchar=true)")
    return True


def time_window_sql(hour_expression: str) -> str:
    case_expressions = " ".join(
        f"WHEN {hour_expression} >= {start_hour} AND {hour_expression} < {end_hour} THEN '{window_name}'"
        for window_name, start_hour, end_hour in TIME_WINDOWS
    )
    # GTFS uses hours above 24 for trips that continue after midnight.
    return f"CASE {case_expressions.replace(hour_expression, f'({hour_expression} % 24)')} END"


def build_stop_departures(connection: duckdb.DuckDBPyConnection, mode: str, directory: Path, service_id: str) -> None:
    for table_name in ["stops", "routes", "trips", "stop_times"]:
        load_csv(connection, directory, f"{table_name}.txt", table_name)
    has_frequencies = load_csv(connection, directory, "frequencies.txt", "frequencies")

    # Departure time in seconds from midnight on the service day.
    connection.sql("""
        CREATE OR REPLACE TABLE trip_stops AS
        SELECT st.trip_id, st.stop_id, t.route_id,
               split_part(st.departure_time, ':', 1)::int * 3600 + split_part(st.departure_time, ':', 2)::int * 60
                 + split_part(st.departure_time, ':', 3)::int AS departure_seconds,
               st.stop_sequence::int AS stop_sequence
        FROM stop_times st
        JOIN trips t USING (trip_id)
        WHERE t.service_id = $service_id AND st.departure_time IS NOT NULL AND st.departure_time <> ''
    """, params={"service_id": service_id})

    if has_frequencies:
        # Frequency-based trips (subway): each stop_times trip is a template repeated every headway_secs
        # between start_time and end_time. Each stop departure is shifted by its offset from the template.
        connection.sql("""
            CREATE OR REPLACE TABLE departures AS
            WITH schedule_template AS (
                SELECT p.*, p.departure_seconds - min(p.departure_seconds) OVER (PARTITION BY p.trip_id) AS offset_seconds
                FROM trip_stops p
            ),
            frequency_windows AS (
                SELECT trip_id,
                       split_part(start_time, ':', 1)::int * 3600 + split_part(start_time, ':', 2)::int * 60 AS start_seconds,
                       split_part(end_time, ':', 1)::int * 3600 + split_part(end_time, ':', 2)::int * 60 AS end_seconds,
                       headway_secs::int AS headway_seconds
                FROM frequencies
            )
            SELECT template.stop_id, template.route_id,
                   windows.start_seconds + departures.iteration * windows.headway_seconds + template.offset_seconds AS departure_seconds
            FROM schedule_template template
            JOIN frequency_windows windows USING (trip_id)
            CROSS JOIN LATERAL (
                SELECT unnest(range(0, ((windows.end_seconds - windows.start_seconds) / windows.headway_seconds)::int + 1)) AS iteration
            ) departures
        """)
    else:
        connection.sql("CREATE OR REPLACE TABLE departures AS SELECT stop_id, route_id, departure_seconds FROM trip_stops")

    connection.sql(f"""
        INSERT INTO transit_summary
        SELECT '{mode}' AS modo, d.stop_id AS parada_id, any_value(p.stop_name) AS parada,
               any_value(p.stop_lat)::double AS latitud, any_value(p.stop_lon)::double AS longitud,
               coalesce(nullif(any_value(r.route_short_name), ''), any_value(r.route_long_name)) AS linea,
               {time_window_sql('(d.departure_seconds // 3600)')} AS franja,
               count(*) AS salidas
        FROM departures d
        JOIN stops p USING (stop_id)
        JOIN routes r USING (route_id)
        GROUP BY d.stop_id, d.route_id, franja
    """)


def build_route_shapes(connection: duckdb.DuckDBPyConnection, mode: str, directory: Path, service_id: str) -> None:
    """Adds the shapes of the routes running on the weekday service, as WKT lines in shape order."""
    shapes_file = directory / "shapes.txt"
    if not shapes_file.exists() or shapes_file.stat().st_size == 0:
        print(f"  {mode}: the feed has no shapes, skipped.")
        return
    for table_name in ["routes", "trips", "shapes"]:
        load_csv(connection, directory, f"{table_name}.txt", table_name)

    connection.sql(f"""
        INSERT INTO transit_routes
        SELECT '{mode}' AS modo,
               coalesce(nullif(any_value(r.route_short_name), ''), any_value(r.route_long_name)) AS linea,
               s.shape_id,
               'LINESTRING(' || string_agg(s.shape_pt_lon || ' ' || s.shape_pt_lat, ',' ORDER BY s.shape_pt_sequence::int) || ')' AS geom_wkt
        FROM shapes s
        JOIN (SELECT DISTINCT route_id, shape_id FROM trips WHERE service_id = $service_id AND coalesce(shape_id, '') <> '') t USING (shape_id)
        JOIN routes r USING (route_id)
        GROUP BY t.route_id, s.shape_id
        HAVING count(*) >= 2
    """, params={"service_id": service_id})


def main() -> int:
    routes_only = "--routes-only" in sys.argv
    DATA_DIR.mkdir(exist_ok=True)
    started_at = time.time()
    connection = duckdb.connect()
    connection.sql("""
        CREATE TABLE transit_summary (modo text, parada_id text, parada text, latitud double, longitud double,
                                     linea text, franja text, salidas integer)
    """)
    connection.sql("CREATE TABLE transit_routes (modo text, linea text, recorrido_id text, geom_wkt text)")

    for mode, (file_name, url, service_id) in FEEDS.items():
        print(f"Processing {mode}...")
        directory = extract_feed(mode, download(file_name, url))
        if not routes_only:
            build_stop_departures(connection, mode, directory, service_id)
        build_route_shapes(connection, mode, directory, service_id)

    if not routes_only:
        destination = DATA_DIR / "transporte_salidas.csv"
        connection.sql(f"COPY (SELECT * FROM transit_summary WHERE franja IS NOT NULL) TO '{destination.as_posix()}' (HEADER)")
        print(connection.sql("SELECT modo, count(DISTINCT parada_id) AS stops, count(DISTINCT linea) AS routes, sum(salidas) AS departures FROM transit_summary GROUP BY 1"))

    routes_destination = DATA_DIR / "transporte_recorridos.csv"
    connection.sql(f"COPY transit_routes TO '{routes_destination.as_posix()}' (HEADER)")
    print(connection.sql("SELECT modo, count(DISTINCT linea) AS routes, count(*) AS shapes FROM transit_routes GROUP BY 1"))
    print(f"Completed in {time.time() - started_at:.0f} s: {routes_destination}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
