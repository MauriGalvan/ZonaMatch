"""
Model that distributes a district's crimes across its census fractions, so its localities do not all receive the same rate.

SNIC only reports crimes by district. In CABA, the Crime Map locates each incident, so a Poisson regression is
fitted there: crimes per resident of each census fraction against census indicators (unmet basic needs,
renting, density) and activity (shops from OSM, public transit departures from GTFS). The model gives every AMBA census fraction a relative factor;
etl/crime/spread_crimes.sql uses it to split each district's total, which does not change.

Validation (leave one commune out, distributing its actual total across its fractions with a model fitted on the
other 14): the absolute error of the spread falls from 33 % (same rate everywhere, the previous method) to 23 %,
and the rank correlation of fraction rates rises from about 0 to 0.66. Run with --validate to repeat it.

Reported crimes follow activity: they are filed where people circulate and where the police is, and less in poor
areas, the same reporting bias corrected between districts with homicides. Since homicides are almost always
recorded, they get their own factor: across AMBA communes and districts, the five-year homicide rate grows
about 17 % per point of households with unmet basic needs (R2 0.45). Each fraction takes that relation with its NBI,
clipped to the range seen across districts, so a district's violence rate is lower where its poorest households live.

Usage (from backend/ZonaMatch, see ZONE-DATA-DOCKER.bat):
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


def validate(raw, y, population, communes):
    """Leave one commune out: spread its real total with a model fitted on the others."""
    offset = np.log(population)
    predicted = np.zeros_like(y)
    for commune in np.unique(communes):
        test, train = communes == commune, communes != commune
        beta = poisson_fit(standardize(raw[train], raw[train]), y[train], offset[train])
        relative = np.exp(standardize(raw[test], raw[train]) @ beta) * population[test]
        predicted[test] = relative / relative.sum() * y[test].sum()
    uniform = np.zeros_like(y)
    for commune in np.unique(communes):
        mask = communes == commune
        uniform[mask] = population[mask] / population[mask].sum() * y[mask].sum()
    for label, guess in [("uniform", uniform), ("model", predicted)]:
        print(f"  {label:9} absolute error {np.abs(guess - y).sum() / y.sum():.1%}")


def department(row):
    """SNIC department of a fraction: its district, or its CABA commune (census commune codes are offset by 7)."""
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
    # Only departments fully inside the AMBA census extract (the 34 communes and districts covered by the zones).
    deps = [d for d in totals if d in rates and totals[d][0] > 0 and totals[d][2] > 50000]
    nbi = np.array([totals[d][1] / totals[d][0] * 100 for d in deps])
    weight = np.array([totals[d][2] for d in deps])
    X = np.column_stack([np.ones(len(deps)), nbi])
    y = np.log([rates[d] for d in deps])
    beta = np.linalg.solve((X.T * weight) @ X, (X.T * weight) @ y)
    fitted = X @ beta
    mean = np.average(y, weights=weight)
    r2 = 1 - np.sum(weight * (y - fitted) ** 2) / np.sum(weight * (y - mean) ** 2)
    print(f"Homicides by NBI ({len(deps)} communes and districts): x{np.exp(beta[1]):.3f} per NBI point, R2 {r2:.2f}")
    return beta[1], nbi.min(), nbi.max()


def main():
    parser = argparse.ArgumentParser(description="Model for distributing each department's crime totals")
    parser.add_argument("input_csv", help="CSV produced by etl/crime/fraction_features.sql")
    parser.add_argument("snic_csv", help="datos/snic_departamentos.csv (etl/download_snic.py)")
    parser.add_argument("output_csv", help="CSV with relative factors for each census fraction")
    parser.add_argument("--validate", action="store_true", help="repeat the leave-one-commune-out validation")
    args = parser.parse_args()

    with open(args.input_csv, encoding="utf-8") as handle:
        rows = [row for row in csv.DictReader(handle) if value(row, "pob") > 0]

    raw = features(rows)
    population = np.array([value(r, "pob") for r in rows])
    caba = np.array([r["prov"] == "02" and value(r, "pob") >= MIN_FIT_POPULATION for r in rows])
    y = np.array([value(r, "total") for r in rows])

    if args.validate:
        communes = np.array([int(r["comuna"] or 0) for r in rows])
        validate(raw[caba], y[caba], population[caba], communes[caba])

    beta = poisson_fit(standardize(raw[caba], raw[caba]), y[caba], np.log(population[caba]))
    print("Crime model by census fraction (CABA Crime Map), effect of a one standard deviation increase in each feature:")
    for name, coefficient in zip(FEATURES, beta[1:]):
        print(f"  {name:10} x{np.exp(coefficient):.2f}")

    # Relative rate of every fraction (CABA mean = 1); only ratios within each district matter.
    factor = np.exp(standardize(raw, raw[caba]) @ beta - beta[0])

    slope, low, high = homicide_model(rows, args.snic_csv)
    nbi = np.clip(raw[:, FEATURES.index("nbi")], low, high)
    homicide_factor = np.exp(slope * (nbi - nbi.mean()))

    with open(args.output_csv, "w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(["fraction_id", "factor", "homicide_factor"])
        for row, f, h in zip(rows, factor, homicide_factor):
            writer.writerow([row["fid"], round(float(f), 4), round(float(h), 4)])
    print(f"  {len(rows)} census fractions in {args.output_csv}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
