using System.Globalization;

namespace ZonaMatch.Domain.Scoring
{
    // User input for scoring: criteria and priorities, places, and limits.
    // The monthly budget is in Argentine pesos: rent plus building expenses for renters, and expenses only for buyers.
    // The purchase price limit is in US dollars. Without a housing type, all types are considered.
    // If the user provides a desired area, prices are estimated for that area. With a number of rooms (1 to 5, 5 = 5 or
    // more), prices are those of that size in each zone (and with an area, its price per square meter).
    public sealed record ScoringRequest(
        IReadOnlyDictionary<string, string> Criteria,
        IReadOnlyList<UserPlace> Places,
        decimal? MaximumMonthlyBudget,
        string Operation = HousingOperations.Rent,
        decimal? MaximumPurchasePriceUsd = null,
        string? HousingType = null,
        int? AreaM2 = null,
        int? Rooms = null)
    {
        public bool IsPurchase => Operation == HousingOperations.Purchase;
    }

    // A place the user visits regularly. Travel time cannot be measured without coordinates.
    public sealed record UserPlace(string Name, double? Latitude, double? Longitude, string? Priority, int MaximumTravelTimeMinutes)
    {
        public bool HasLocation => Latitude is not null && Longitude is not null;
    }

    // Trip duration; minutes are null when no route is available. Estimated when routing did not calculate it.
    public sealed record TravelTime(
        int? Minutes,
        bool IsEstimated,
        double? DistanceKm = null);

    // Route distance in kilometers; null when only travel time could be estimated.
    public sealed record PlaceScoreResult(
        string Name,
        string? Priority,
        int? Minutes,
        int MaximumMinutes,
        bool IsEstimated,
        int? Score,
        double? DistanceKm = null);

    public sealed record ScoreComponent(string Criterion, string Priority, int? Score, string Details);
    public sealed record LimitResult(string Type, bool? IsSatisfied, string Details);
    public sealed record ZoneScoreResult(
        ZoneProfile Zone,
        int? Score,
        IReadOnlyList<ScoreComponent> Components,
        IReadOnlyList<LimitResult> Limits,
        IReadOnlyList<PlaceScoreResult> Places);

    // Calculate a zone's score (0 to 100) for a request. The engine is stateless between calculations and can be shared.
    // Each criterion is the zone's percentile among zones of the same type (neighborhoods and localities, or communes
    // and districts): 80 means the zone scores higher than 80% of peers for that criterion.
    // The total is a weighted average based on the user's priority (low 1, medium 3, high 9; each level weighs three
    // times the previous one). Criteria without data, such as safety outside CABA, neither add nor subtract points.
    // The user's places add a separate high-priority criterion based on travel time.
    // Limits cover budgets and maximum travel times. A small overage deducts one point per 1%; a larger overage reduces
    // the remaining score exponentially. A limit without data does not affect the score.
    public sealed class ScoringEngine
    {
        public const string Safety = "seguridad";
        public const string Mobility = "movilidad";
        public const string GreenSpaces = "espacios-verdes";
        public const string Education = "educacion";
        public const string Health = "salud";
        public const string Places = "lugares";

        public const string RentLimit = "alquiler";
        public const string ExpensesLimit = "expensas";
        public const string PurchaseLimit = "compra";
        public const string TravelTimeLimit = "tiempo";

        // In a group analysis, the group sets budget limits instead of individual participants.
        public static bool IsBudgetLimit(string type) => type is RentLimit or ExpensesLimit or PurchaseLimit;

        // The penalty is linear up to this overage (20%); each additional 20% overage divides the remaining score by e.
        public const double MinorOverage = 0.20;
        public const double OverageScale = 0.20;

        // Morning peak hours (6 to 9) used to count peak-hour departures.
        private const int PeakHours = 3;

        private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-AR");

        private static readonly IReadOnlyDictionary<string, int> PriorityWeights = new Dictionary<string, int>
        {
            ["baja"] = 1,
            ["media"] = 3,
            ["alta"] = 9,
        };

        private static readonly IReadOnlyDictionary<string, int> PlacePriorityWeights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Muy importante"] = 4,
            ["Alta"] = 3,
            ["Media"] = 2,
            ["Baja"] = 1,
        };

        // Criterion indicators where higher is better; safety reverses the sign of crime counts.
        private static readonly IReadOnlyDictionary<string, Func<ZoneProfile, double?>> Indicators =
            new Dictionary<string, Func<ZoneProfile, double?>>
            {
                [Safety] = z => -z.CrimesPerThousandResidents,
                [Mobility] = z => z.PeakHourDeparturesPerKm2,
                [GreenSpaces] = z => z.GreenSpaceM2PerResident,
                [Education] = z => z.SchoolsPerTenThousandResidents,
                [Health] = z => z.HealthCentersPerTenThousandResidents,
            };

        // Sorted indicator values by zone type (large or small), used to calculate percentiles.
        private readonly Dictionary<(string Criterion, bool IsLargeZone), double[]> _distributions = [];

        public ScoringEngine(IReadOnlyCollection<ZoneProfile> zoneProfiles)
        {
            foreach (var (criterion, indicator) in Indicators)
                foreach (var isLargeZone in new[] { false, true })
                {
                    _distributions[(criterion, isLargeZone)] = zoneProfiles
                        .Where(zone => zone.IsLargeZone == isLargeZone)
                        .Select(indicator)
                        .OfType<double>()
                        .Order()
                        .ToArray();
                }
        }

        public static bool IsKnownCriterion(string criterion) => Indicators.ContainsKey(criterion);

        // Times are provided for each request place in the same order (null when a place has no location).
        public ZoneScoreResult Calculate(ZoneProfile zone, ScoringRequest request, IReadOnlyList<TravelTime?>? travelTimes = null)
        {
            var components = request.Criteria
                .Where(c => Indicators.ContainsKey(c.Key) && PriorityWeights.ContainsKey(c.Value))
                .Select(c => BuildComponent(zone, c.Key, c.Value))
                .ToList();

            var places = BuildPlaceResults(request.Places, travelTimes);
            if (places.Count > 0)
                components.Add(BuildPlacesComponent(places));

            var limits = new List<LimitResult>();
            var budgetOverage = 0.0;
            var market = zone.GetMarket(request.HousingType, request.Rooms);

            if (request.IsPurchase && request.MaximumPurchasePriceUsd is { } maximumPurchasePriceUsd && maximumPurchasePriceUsd > 0)
            {
                var (limit, overage) = PurchaseLimitFor(market, maximumPurchasePriceUsd, request.HousingType, request.AreaM2);
                limits.Add(limit);
                budgetOverage = Math.Max(budgetOverage, overage);
            }

            if (request.MaximumMonthlyBudget is { } maximumMonthlyBudget && maximumMonthlyBudget > 0)
            {
                var (limit, overage) = request.IsPurchase
                    ? ExpensesLimitFor(market, maximumMonthlyBudget)
                    : RentLimitFor(market, maximumMonthlyBudget, request.HousingType, request.AreaM2);
                limits.Add(limit);
                budgetOverage = Math.Max(budgetOverage, overage);
            }

            if (places.Count > 0)
                limits.AddRange(BuildTravelTimeLimits(places));

            return new ZoneScoreResult(zone, CalculateTotalScore(components, budgetOverage), components, limits, places);
        }

        // Percentile of a value in its distribution (ties count as half), from 0 to 100.
        public static int Percentile(IReadOnlyList<double> sortedValues, double value)
        {
            if (sortedValues.Count == 0)
                return 50;

            var lowerValues = 0;
            var matchingValues = 0;
            foreach (var item in sortedValues)
            {
                if (item < value) lowerValues++;
                else if (item == value) matchingValues++;
            }

            return (int)RoundAwayFromZero(100.0 * (lowerValues + 0.5 * matchingValues) / sortedValues.Count);
        }

        // Score after exceeding a limit (0.1 means 10% over): deduct one point per 1% up to MinorOverage, then reduce the
        // remaining score exponentially. The score stays unchanged when there is no overage.
        public static double ApplyPenalty(double score, double overage)
        {
            if (overage <= 0)
                return score;

            var remainingScore = Math.Max(0, score - Math.Min(overage, MinorOverage) * 100);

            return overage <= MinorOverage ? remainingScore : remainingScore * Math.Exp(-(overage - MinorOverage) / OverageScale);
        }

        // Scores range from 100 (arrives immediately) to 80 (exactly at the limit); beyond the limit, apply the usual
        // penalty. Null when travel time could not be calculated.
        public static int? TravelScore(int? minutes, int maximumMinutes)
        {
            if (minutes is not { } elapsedMinutes)
                return null;

            if (elapsedMinutes <= maximumMinutes)
                return (int)RoundAwayFromZero(100 - 20.0 * elapsedMinutes / maximumMinutes);

            return (int)RoundAwayFromZero(ApplyPenalty(80, (double)elapsedMinutes / maximumMinutes - 1));
        }

        private ScoreComponent BuildComponent(ZoneProfile zone, string criterion, string priority)
        {
            var value = Indicators[criterion](zone);
            int? score = value is { } indicatorValue ? Percentile(_distributions[(criterion, zone.IsLargeZone)], indicatorValue) : null;

            return new ScoreComponent(criterion, priority, score, Details(zone, criterion));
        }

        private static string Details(ZoneProfile zone, string criterion) => criterion switch
        {
            Safety => zone.CrimesPerThousandResidents is { } crimes
                ? $"{crimes.ToString("N1", SpanishCulture)} delitos estimados cada 1.000 hab. en un año, incluidos los no denunciados"
                : "Sin datos de delitos",
            Mobility => $"{zone.TransitLineCount} líneas · {(zone.PeakHourDepartures / PeakHours).ToString("N0", SpanishCulture)} salidas por hora en hora pico",
            GreenSpaces => zone.GreenSpaceM2PerResident is { } greenSpaceArea
                ? $"{greenSpaceArea.ToString("N1", SpanishCulture)} m² de plazas y parques por habitante"
                : "Sin datos de población para comparar",
            Education => $"{zone.SchoolCount.ToString("N0", SpanishCulture)} escuelas y jardines",
            Health => $"{zone.HealthCenterCount.ToString("N0", SpanishCulture)} hospitales, clínicas y centros de salud",
            _ => string.Empty,
        };

        private static List<PlaceScoreResult> BuildPlaceResults(IReadOnlyList<UserPlace> places, IReadOnlyList<TravelTime?>? travelTimes)
        {
            var results = new List<PlaceScoreResult>();

            for (var i = 0; i < places.Count; i++)
            {
                if (travelTimes is null || i >= travelTimes.Count || travelTimes[i] is not { } travelTime)
                    continue;

                var place = places[i];
                results.Add(new PlaceScoreResult(
                    place.Name,
                    place.Priority,
                    travelTime.Minutes,
                    place.MaximumTravelTimeMinutes,
                    travelTime.IsEstimated,
                    TravelScore(travelTime.Minutes, place.MaximumTravelTimeMinutes),
                    travelTime.DistanceKm));
            }

            return results;
        }

        private static ScoreComponent BuildPlacesComponent(IReadOnlyList<PlaceScoreResult> places)
        {
            // Only calculated trips count; places that matter more to the user receive higher weights.
            var measuredPlaces = places.Where(place => place.Score is not null).ToList();
            if (measuredPlaces.Count == 0)
                return new ScoreComponent(Places, "alta", null, "No pudimos calcular los viajes a tus lugares");

            var weights = measuredPlaces.Select(place => GetPlacePriorityWeight(place.Priority)).ToList();
            var score = (int)RoundAwayFromZero(measuredPlaces.Select((place, i) => (double)place.Score!.Value * weights[i]).Sum() / weights.Sum());
            var placesWithinLimit = measuredPlaces.Count(place => place.Minutes <= place.MaximumMinutes);

            return new ScoreComponent(Places, "alta", score, $"{placesWithinLimit} de {measuredPlaces.Count} lugares dentro de tu tiempo máximo");
        }

        // A place without a known priority uses the medium-priority weight.
        private static int GetPlacePriorityWeight(string? priority) =>
            priority is not null && PlacePriorityWeights.TryGetValue(priority, out var weight) ? weight : PlacePriorityWeights["Media"];

        // Return the limit and the overage (0.1 means 10%); missing data does not affect the score.
        // The limit is monthly, so compare it with rent plus building expenses.
        private static (LimitResult Limit, double Overage) RentLimitFor(
            HousingMarket market, decimal maximumBudget, string? housingType, int? areaM2)
        {
            var housingTypeSuffix = HousingTypes.TextSuffix(housingType) + HousingRooms.TextSuffix(market.RentRooms);
            if (market.MonthlyRentCostForArea(areaM2) is not { } monthlyCost)
                return (new LimitResult(RentLimit, null, $"Sin datos de alquiler{housingTypeSuffix} para esta zona"), 0);

            // With a distribution, the limit is compared with the range of what is paid per month (CompareWithDistribution)
            if (market.MonthlyRentDistribution(areaM2) is { } monthly)
            {
                var areaText = areaM2 is > 0 && PriceDistribution.IsValid(market.RentPerM2Deciles) ? $" para {areaM2} m²" : string.Empty;
                var range = $"Alquiler + expensas{housingTypeSuffix}{areaText}: de {FormatArgentinePesos(PriceDistribution.Minimum(monthly))} " +
                            $"a {FormatArgentinePesos(PriceDistribution.Maximum(monthly))} por mes, precio medio {FormatArgentinePesos(PriceDistribution.Median(monthly))}";
                return CompareWithDistribution(RentLimit, monthly, maximumBudget, range, $"tu tope de {FormatArgentinePesos(maximumBudget)}");
            }

            var rent = market.RentForArea(areaM2)!.Value;
            var isEstimated = market.EstimatesRent(areaM2);
            var rentLabel = isEstimated
                ? $"Alquiler estimado{housingTypeSuffix} para {areaM2} m² {FormatArgentinePesos(rent)}"
                : $"Alquiler{housingTypeSuffix} {FormatArgentinePesos(rent)}";
            var details = market.AverageExpenses is { } expenses
                ? $"{rentLabel} + expensas {FormatArgentinePesos(expenses)} = {FormatArgentinePesos(monthlyCost)} por mes"
                : isEstimated
                    ? $"{rentLabel} por mes (sin dato de expensas)"
                    : $"Alquiler promedio{housingTypeSuffix} {FormatArgentinePesos(monthlyCost)} (sin dato de expensas)";

            return CompareWithLimit(RentLimit, monthlyCost, maximumBudget, details, $"tu tope de {FormatArgentinePesos(maximumBudget)}");
        }

        // Buyers do not pay rent; their monthly housing cost is the building expenses.
        private static (LimitResult Limit, double Overage) ExpensesLimitFor(HousingMarket market, decimal maximumBudget)
        {
            if (market.AverageExpenses is not { } expenses)
                return (new LimitResult(ExpensesLimit, null, "Sin datos de expensas para esta zona"), 0);

            return CompareWithLimit(ExpensesLimit, expenses, maximumBudget, $"Expensas promedio {FormatArgentinePesos(expenses)} por mes", $"tu tope mensual de {FormatArgentinePesos(maximumBudget)}");
        }

        private static (LimitResult Limit, double Overage) PurchaseLimitFor(
            HousingMarket market, decimal maximumPriceUsd, string? housingType, int? areaM2)
        {
            var housingTypeSuffix = HousingTypes.TextSuffix(housingType) + HousingRooms.TextSuffix(market.SaleRooms);
            if (market.SalePriceUsdForArea(areaM2) is not { } salePrice)
                return (new LimitResult(PurchaseLimit, null, $"Sin datos de venta{housingTypeSuffix} para esta zona"), 0);

            if (market.SalePriceDistributionUsd(areaM2) is { } prices)
            {
                var areaText = areaM2 is > 0 && PriceDistribution.IsValid(market.SalePerM2DecilesUsd) ? $" para {areaM2} m²" : string.Empty;
                var range = $"Precio{housingTypeSuffix}{areaText}: de {FormatDollars(PriceDistribution.Minimum(prices))} " +
                            $"a {FormatDollars(PriceDistribution.Maximum(prices))}, precio medio {FormatDollars(PriceDistribution.Median(prices))}";
                return CompareWithDistribution(PurchaseLimit, prices, maximumPriceUsd, range, $"tu tope de compra de {FormatDollars(maximumPriceUsd)}");
            }

            var details = market.EstimatesSale(areaM2)
                ? $"Precio estimado{housingTypeSuffix} para {areaM2} m² {FormatDollars(salePrice)} ({FormatDollars(market.SalePricePerM2Usd!.Value)}/m²)"
                : $"Precio promedio{housingTypeSuffix} {FormatDollars(salePrice)}";

            return CompareWithLimit(PurchaseLimit, salePrice, maximumPriceUsd, details, $"tu tope de compra de {FormatDollars(maximumPriceUsd)}");
        }

        // Limit against the price distribution, on the same scale as ApplyPenalty:
        // - every listing fits: no deduction;
        // - some listings fit: the limit is met and up to MinorOverage is deducted for the share that does not fit;
        // - none fits (the limit is below the cheapest listing): MinorOverage is deducted and the score falls from there
        //   like any other limit.
        // This way the score does not jump when the limit moves.
        private static (LimitResult Limit, double Overage) CompareWithDistribution(
            string type, decimal[] deciles, decimal maximum, string details, string limitDescription)
        {
            var minimum = PriceDistribution.Minimum(deciles);
            if (maximum < minimum)
            {
                var shortfall = (double)(minimum / maximum - 1);
                return (new LimitResult(type, false,
                    $"{details}: ninguna entra, la más barata supera {limitDescription} en un {(int)Math.Ceiling(shortfall * 100)} %"), MinorOverage + shortfall);
            }

            var percentage = PriceDistribution.PercentageUpTo(deciles, maximum);
            var text = percentage >= 100
                ? $"{details}: todas entran en {limitDescription}"
                : $"{details}: entra el {percentage} % de las publicaciones en {limitDescription}";
            return (new LimitResult(type, true, text), MinorOverage * (100 - percentage) / 100.0);
        }

        private static (LimitResult Limit, double Overage) CompareWithLimit(string type, decimal value, decimal maximum, string details, string limitDescription)
        {
            if (value <= maximum)
                return (new LimitResult(type, true, $"{details}: dentro de {limitDescription}"), 0);

            var overage = (double)(value / maximum - 1);
            var percentage = (int)Math.Ceiling(overage * 100);

            return (new LimitResult(type, false, $"{details}: supera {limitDescription} en un {percentage} %"), overage);
        }

        private static IEnumerable<LimitResult> BuildTravelTimeLimits(IReadOnlyList<PlaceScoreResult> places)
        {
            var overLimitPlaces = places.Where(place => place.Minutes > place.MaximumMinutes).ToList();
            var unmeasuredPlaces = places.Where(place => place.Minutes is null).ToList();

            if (overLimitPlaces.Count == 0 && unmeasuredPlaces.Count < places.Count)
                yield return new LimitResult(TravelTimeLimit, true,
                    unmeasuredPlaces.Count == 0 ? "Llegás a todos tus lugares dentro de tu tiempo máximo" : "Llegás a tiempo a los lugares que pudimos medir");

            foreach (var place in overLimitPlaces)
                yield return new LimitResult(TravelTimeLimit, false, $"A {place.Name}: {place.Minutes} min (tu máximo es {place.MaximumMinutes} min)");

            foreach (var place in unmeasuredPlaces)
                yield return new LimitResult(TravelTimeLimit, null, $"A {place.Name}: no pudimos calcular el viaje");
        }

        private static int? CalculateTotalScore(IReadOnlyList<ScoreComponent> components, double budgetOverage)
        {
            var scoredComponents = components.Where(component => component.Score is not null).ToList();
            if (scoredComponents.Count == 0)
                return null;

            var totalWeight = scoredComponents.Sum(component => PriorityWeights[component.Priority]);
            var weightedAverage = scoredComponents.Sum(component => component.Score!.Value * PriorityWeights[component.Priority]) / (double)totalWeight;

            return (int)Math.Clamp(RoundAwayFromZero(ApplyPenalty(weightedAverage, budgetOverage)), 0, 100);
        }

        // Round midpoint values away from zero (12.5 -> 13), instead of to even as Math.Round does by default.
        private static double RoundAwayFromZero(double value) => Math.Round(value, MidpointRounding.AwayFromZero);

        private static string FormatArgentinePesos(decimal amount) => $"$ {amount.ToString("N0", SpanishCulture)}";

        private static string FormatDollars(decimal amount) => $"US$ {amount.ToString("N0", SpanishCulture)}";
    }
}
