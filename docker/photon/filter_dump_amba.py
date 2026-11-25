#!/usr/bin/env python3
"""Recorta un dump JSON de Photon (jsonl) a un recuadro geografico (por defecto, el AMBA).

Uso:
    python filter_dump_amba.py ENTRADA.jsonl SALIDA.jsonl [minLon minLat maxLon maxLat]
    zstd -dc dump.jsonl.zst | python filter_dump_amba.py - SALIDA.jsonl      (ENTRADA "-" = stdin)

Conserva la cabecera y la informacion de paises, y de cada linea "Place" deja solo los lugares cuyo
centroid cae dentro del recuadro. Lee y escribe en UTF-8. Procesa de a una linea, sin cargar el archivo.
"""
import json
import os
import sys
from pathlib import Path


def load_amba_bounds():
    config_path = Path(__file__).with_name("amba-bounds.json")
    with config_path.open(encoding="utf-8") as config_file:
        bounds = json.load(config_file)
    return (
        bounds["MinLongitude"],
        bounds["MinLatitude"],
        bounds["MaxLongitude"],
        bounds["MaxLatitude"],
    )


def inside(place, bbox):
    centroid = place.get("centroid")
    if not centroid or len(centroid) != 2:
        return False
    lon, lat = centroid
    return bbox[0] <= lon <= bbox[2] and bbox[1] <= lat <= bbox[3]


def main():
    if len(sys.argv) not in (3, 7):
        sys.exit(__doc__)
    source, target = sys.argv[1], sys.argv[2]
    try:
        bbox = tuple(float(v) for v in sys.argv[3:7]) if len(sys.argv) == 7 else load_amba_bounds()
        west, south, east, north = bbox
        if not (-180 <= west < east <= 180 and -90 <= south < north <= 90):
            raise ValueError("Expected ordered WGS84 longitude and latitude limits.")
    except (OSError, ValueError, KeyError, TypeError) as error:
        sys.exit(f"Invalid crop bounds: {error}")

    os.makedirs(os.path.dirname(os.path.abspath(target)), exist_ok=True)

    read = kept_lines = kept_places = dropped_places = 0
    if source == "-":
        sys.stdin.reconfigure(encoding="utf-8")
    src_ctx = sys.stdin if source == "-" else open(source, encoding="utf-8")
    with src_ctx as src, open(target, "w", encoding="utf-8", newline="\n") as dst:
        for raw in src:
            line = raw.strip()
            if not line:
                continue
            read += 1
            entry = json.loads(line)
            if entry.get("type") == "Place":
                places = entry.get("content") or []
                selected = [p for p in places if inside(p, bbox)]
                dropped_places += len(places) - len(selected)
                if not selected:
                    continue
                kept_places += len(selected)
                entry["content"] = selected
            dst.write(json.dumps(entry, ensure_ascii=False, separators=(",", ":")) + "\n")
            kept_lines += 1
            if read % 500000 == 0:
                print(f"{read:,} lineas leidas, {kept_places:,} lugares conservados", flush=True)

    print(f"Listo: {read:,} lineas leidas, {kept_lines:,} escritas, {kept_places:,} lugares conservados, "
          f"{dropped_places:,} descartados")


if __name__ == "__main__":
    main()
