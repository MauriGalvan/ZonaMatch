"""
Model that spreads the crimes of a partido over its census fractions, so its localidades are not all the same.

The SNIC only has crimes per partido. In CABA the Mapa del Delito locates each event, so a Poisson regression is
fitted there: crimes per resident of each census fraction against census indicators (unmet basic needs,
renting, density) and activity (shops from OSM, public transport departures from the GTFS). The model gives every fraction of the AMBA a relative factor;
etl/crime/spread_crimes.sql uses it to split each partido's total, which does not change.

Validation (leave one comuna out, spreading each comuna's real total over its fractions with a model fitted on the
other 14): the absolute error of the spread falls from 33 % (same rate everywhere, the previous method) to 23 %,
and the rank correlation of the fractions' rates rises from about 0 to 0.66. Run with --validate to repeat it.

Reported crimes follow activity: they are filed where people circulate and where the police is, and less in poor
areas, the same reporting bias corrected between partidos with homicides. So homicides, which are almost always
recorded, get their own factor: across the comunas and partidos of the AMBA, the five-year homicide rate grows
about 17 % per point of households with unmet basic needs (R2 0.45). Each fraction takes that relation with its NBI,
clipped to the range seen between partidos, so the violence of a partido falls where its poorest households live.

Usage (from backend/ZonaMatch, see DATOS-ZONAS-DOCKER.bat):
  python etl/crime/fit_crime_model.py datos/delitos_fracciones_entrada.csv datos/snic_departamentos.csv
      datos/delitos_fracciones.csv
Requires numpy (pip install numpy).
"""

import argparse
import csv
import sys

import numpy as np

# Poverty enters through unmet basic needs (NBI) only: overcrowding, health coverage and university studies measure
# almost the same and, with all of them in, the fit gives the weight to one of them and leaves NBI near zero. With NBI
# alone the error barely changes (22.9 % against 21.9 %) and the effect is the expected one: more NBI, more crime.
# The RENABAP share of households in barrios populares (by barrio) did not improve it: NBI already captures it.
FEATURES = ["nbi", "alq", "dens", "comercios", "salidas"]

# Fractions with very few residents (a park, a station) have meaningless rates: they are left out of the fit
MIN_FIT_POPULATION = 300


def value(row, key):
    return float(row[key] or 0)


def features(rows):
    """Matrix of the model variables, one row per fraction."""
    def pct(row, part, total):
        return value(row, part) / max(value(row, total), 1) * 100

    columns = {
        "nbi": [pct(r, "nbi", "hogares") for r in rows],
        "hac": [pct(r, "hac", "hogares") for r in rows],
        "alq": [pct(r, "alq", "hogares") for r in rows],
        "univ": [pct(r, "univ", "edu") for r in rows],
        "cob": [pct(r, "cob", "cobt") for r in rows],
        "dens": [np.log(max(value(r, "pob"), 1) / max(value(r, "km2"), 0.01)) for r in rows],
        "comercios": [np.log1p(value(r, "comercios") / max(value(r, "pob"), 1) * 1000) for r in rows],
        "salidas": [np.log1p(value(r, "salidas") / max(value(r, "pob"), 1)) for r in rows],
    }
    return np.column_stack([columns[name] for name in FEATURES])


def standardize(raw, reference):
    """Scale with the CABA ranges; values outside them are clipped, so the model does not extrapolate."""
    low, high = reference.min(axis=0), reference.max(axis=0)
    mean, std = reference.mean(axis=0), reference.std(axis=0)
    std[std == 0] = 1
    clipped = np.clip(raw, low, high)
    return np.column_stack([np.ones(len(raw)), (clipped - mean) / std])


def poisson_fit(X, y, offset, iterations=100):
    """Poisson regression with a log(population) offset, by iteratively reweighted least squares."""
    beta = np.zeros(X.shape[1])
    beta[0] = np.log(y.sum() / np.exp(offset).sum())
    for _ in range(iterations):
        eta = X @ beta + offset
        mu = np.exp(eta)
        z = eta - offset + (y - mu) / mu
        weighted = X.T * mu
        updated = np.linalg.solve(weighted @ X + 1e-6 * np.eye(X.shape[1]), weighted @ z)
        if np.max(np.abs(updated - beta)) < 1e-9:
            return updated
        beta = updated
    return beta


def validate(raw, y, population, comuna):
    """Leave one comuna out: spread its real total with a model fitted on the others."""
    offset = np.log(population)
    predicted = np.zeros_like(y)
    for c in np.unique(comuna):
        test, train = comuna == c, comuna != c
        beta = poisson_fit(standardize(raw[train], raw[train]), y[train], offset[train])
        relative = np.exp(standardize(raw[test], raw[train]) @ beta) * population[test]
        predicted[test] = relative / relative.sum() * y[test].sum()
    uniform = np.zeros_like(y)
    for c in np.unique(comuna):
        mask = comuna == c
        uniform[mask] = population[mask] / population[mask].sum() * y[mask].sum()
    for label, guess in [("uniforme", uniform), ("modelo", predicted)]:
        print(f"  {label:9} error absoluto {np.abs(guess - y).sum() / y.sum():.1%}")


def department(row):
    """SNIC department of a fraction: the partido, or the comuna in CABA (the census numbers comunas by 7)."""
    fid = row["fid"]
    if fid.startswith("02"):
        return "02" + str(int(fid[2:5]) // 7).zfill(3)
    return fid[:5]


def homicide_model(rows, snic_file):
    """Log of the five-year homicide rate against NBI %, across departments weighted by population."""
    with open(snic_file, encoding="utf-8") as handle:
        rates = {r["department_id"]: float(r["rate_per_100k"]) for r in csv.DictReader(handle)
                 if r["crime_group"] == "homicides_5y" and float(r["rate_per_100k"]) > 0}
    totals = {}
    for row in rows:
        dep = department(row)
        households, nbi, pob = totals.get(dep, (0.0, 0.0, 0.0))
        totals[dep] = (households + value(row, "hogares"), nbi + value(row, "nbi"), pob + value(row, "pob"))
    # Only departments fully inside the AMBA extract of the census (the 34 comunas and partidos of the zones)
    deps = [d for d in totals if d in rates and totals[d][0] > 0 and totals[d][2] > 50000]
    nbi = np.array([totals[d][1] / totals[d][0] * 100 for d in deps])
    weight = np.array([totals[d][2] for d in deps])
    X = np.column_stack([np.ones(len(deps)), nbi])
    y = np.log([rates[d] for d in deps])
    beta = np.linalg.solve((X.T * weight) @ X, (X.T * weight) @ y)
    fitted = X @ beta
    mean = np.average(y, weights=weight)
    r2 = 1 - np.sum(weight * (y - fitted) ** 2) / np.sum(weight * (y - mean) ** 2)
    print(f"Homicidios segun NBI ({len(deps)} comunas y partidos): x{np.exp(beta[1]):.3f} por punto de NBI, R2 {r2:.2f}")
    return beta[1], nbi.min(), nbi.max()


def main():
    parser = argparse.ArgumentParser(description="Modelo para repartir los delitos de cada partido")
    parser.add_argument("entrada", help="CSV de etl/crime/fraction_features.sql")
    parser.add_argument("snic", help="datos/snic_departamentos.csv (etl/download_snic.py)")
    parser.add_argument("salida", help="CSV con los factores relativos de cada fraccion")
    parser.add_argument("--validate", action="store_true", help="repetir la validacion por comuna")
    args = parser.parse_args()

    with open(args.entrada, encoding="utf-8") as handle:
        rows = [row for row in csv.DictReader(handle) if value(row, "pob") > 0]

    raw = features(rows)
    population = np.array([value(r, "pob") for r in rows])
    caba = np.array([r["prov"] == "02" and value(r, "pob") >= MIN_FIT_POPULATION for r in rows])
    y = np.array([value(r, "total") for r in rows])

    if args.validate:
        comuna = np.array([int(r["comuna"] or 0) for r in rows])
        validate(raw[caba], y[caba], population[caba], comuna[caba])

    beta = poisson_fit(standardize(raw[caba], raw[caba]), y[caba], np.log(population[caba]))
    print("Modelo de delitos por fraccion (CABA, Mapa del Delito), efecto de un desvio de cada variable:")
    for name, coefficient in zip(FEATURES, beta[1:]):
        print(f"  {name:10} x{np.exp(coefficient):.2f}")

    # Relative rate of every fraction (CABA mean = 1); only the ratios within each partido matter
    factor = np.exp(standardize(raw, raw[caba]) @ beta - beta[0])

    slope, low, high = homicide_model(rows, args.snic)
    nbi = np.clip(raw[:, FEATURES.index("nbi")], low, high)
    homicide_factor = np.exp(slope * (nbi - nbi.mean()))

    with open(args.salida, "w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(["fraction_id", "factor", "homicide_factor"])
        for row, f, h in zip(rows, factor, homicide_factor):
            writer.writerow([row["fid"], round(float(f), 4), round(float(h), 4)])
    print(f"  {len(rows)} fracciones en {args.salida}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
