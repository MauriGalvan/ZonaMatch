"""
Downloads the crime statistics by partido of the Sistema Nacional de Informacion Criminal (SNIC, Ministerio de
Seguridad de la Nacion) and keeps what the zone detail and the scoring use: CABA and Buenos Aires province, the
latest year, and the same crime groups as the CABA Mapa del Delito.

Source: https://datos.gob.ar/dataset/snic-departamental-estadisticas-criminales-en-la-republica-argentina-por-departamentos
These are crimes reported to the police, aggregated by partido (comuna in CABA); there is no finer location.
etl/calcular_zonas.sql spreads them over the localidades of each partido by population.

Output: datos/snic_departamentos.csv with year, department_id (INDEC code, as in the census radios),
department_name, crime_group, events and rate_per_100k (rate on the population projected for that year). The
group "homicides_5y" is the yearly average of the last five years of homicides: homicides are almost always
recorded, so etl/calcular_zonas.sql uses them to correct the differences in reporting between partidos.

Usage: python etl/download_snic.py   (from backend/ZonaMatch). No dependencies beyond the standard library.
"""

import csv
import io
import sys
import time
import urllib.error
import urllib.request
from collections import defaultdict
from pathlib import Path

SNIC_URL = "https://cloud-snic.minseg.gob.ar/Bases/SNIC/snic-departamentos-anual.csv"
OUTPUT = Path(__file__).resolve().parent.parent / "datos" / "snic_departamentos.csv"
USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ZonaMatch-ETL/1.0"

# Years of homicides averaged into "homicides_5y": a single year has few homicides per partido or comuna
HOMICIDE_YEARS = 5
HOMICIDE_CODE = "1"

# CABA and Buenos Aires province (INDEC province codes)
PROVINCES = {"02", "06"}

# SNIC crime codes per group, attempts included. The first five add up to the crime total of a zone, as the
# Robo, Hurto, Lesiones, Homicidios and Amenazas types of the Mapa del Delito do; "road" is reported apart.
CRIME_GROUPS = {
    "1": "homicides",      # Homicidios dolosos
    "5": "injuries",       # Lesiones dolosas
    "13": "threats",       # Amenazas
    "15": "robberies",     # Robos
    "16": "robberies",     # Tentativas de robo
    "17": "robberies",     # Robos agravados por el resultado de lesiones y/o muertes
    "18": "robberies",     # Tentativas de robo agravado
    "19": "thefts",        # Hurtos
    "20": "thefts",        # Tentativas de hurto
    "3": "road",           # Muertes en accidentes viales
    "6": "road",           # Lesiones culposas en accidentes viales
}


# Codes the SNIC publishes differently from INDEC (the census): Quilmes comes as 06058 instead of 06658
DEPARTMENT_ID_FIXES = {"06058": "06658"}


def number(value):
    # Decimals come with a comma in some rows ("5,97")
    return float(value.replace(",", ".")) if value not in ("", "NA") else 0.0


def download():
    """Whole file in memory, retrying: the server sometimes drops long connections."""
    request = urllib.request.Request(SNIC_URL, headers={"User-Agent": USER_AGENT})
    for attempt in range(1, 4):
        try:
            with urllib.request.urlopen(request, timeout=600) as response:
                return response.read()
        except (urllib.error.URLError, ConnectionError, TimeoutError) as error:
            if attempt == 3:
                raise
            print(f"  Fallo la descarga ({error}); reintentando...")
            time.sleep(10 * attempt)


def main():
    print("Estadisticas criminales por partido (SNIC, Ministerio de Seguridad), unos 70 MB...")
    rows = [row for row in csv.DictReader(io.StringIO(download().decode("utf-8")), delimiter=";")
            if row["provincia_id"] in PROVINCES and row["codigo_delito_snic_id"] in CRIME_GROUPS]

    year = max(int(row["anio"]) for row in rows)
    totals = defaultdict(lambda: [0.0, 0.0])
    names = {}
    for row in rows:
        if int(row["anio"]) != year:
            continue
        department_id = DEPARTMENT_ID_FIXES.get(row["departamento_id"], row["departamento_id"])
        key = (department_id, CRIME_GROUPS[row["codigo_delito_snic_id"]])
        totals[key][0] += number(row["cantidad_hechos"])
        totals[key][1] += number(row["tasa_hechos"])
        names[department_id] = row["departamento_nombre"]

    # Homicides of the last HOMICIDE_YEARS years: average events and average rate per year
    for row in rows:
        if row["codigo_delito_snic_id"] != HOMICIDE_CODE or int(row["anio"]) <= year - HOMICIDE_YEARS:
            continue
        department_id = DEPARTMENT_ID_FIXES.get(row["departamento_id"], row["departamento_id"])
        key = (department_id, "homicides_5y")
        totals[key][0] += number(row["cantidad_hechos"]) / HOMICIDE_YEARS
        totals[key][1] += number(row["tasa_hechos"]) / HOMICIDE_YEARS

    OUTPUT.parent.mkdir(exist_ok=True)
    with OUTPUT.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(["year", "department_id", "department_name", "crime_group", "events", "rate_per_100k"])
        for (department_id, group), (events, rate) in sorted(totals.items()):
            writer.writerow([year, department_id, names[department_id], group, round(events), round(rate, 2)])

    print(f"  Anio {year}: {len(names)} partidos y comunas en {OUTPUT}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
