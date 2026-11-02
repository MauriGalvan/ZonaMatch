using System.Globalization;

namespace ZonaMatch.Domain.Scoring
{
    // User input for scoring: criteria and priorities, places, and limits.
    // The monthly budget is in Argentine pesos: rent plus building expenses for renters, and expenses only for buyers.
    // The purchase price limit is in US dollars. Without a housing type, all types are considered.
    // If the user provides a desired area, prices are estimated for that area.
    public sealed record ScoringRequest(
        IReadOnlyDictionary<string, string> Criterios,
        IReadOnlyList<UserPlace> Lugares,
        decimal? GastoMensualMaximo,
        string Operacion = HousingOperations.Rent,
        decimal? CompraMaximaUsd = null,
        string? TipoVivienda = null,
        int? SuperficieM2 = null)
    {
        public bool EsCompra => Operacion == HousingOperations.Purchase;
    }

    // A place the user visits regularly. Travel time cannot be measured without coordinates.
    public sealed record UserPlace(string Nombre, double? Latitud, double? Longitud, string? Prioridad, int TiempoMaximoMinutos)
    {
        public bool TieneUbicacion => Latitud is not null && Longitud is not null;
    }

    // Trip duration; minutes are null when no route is available. Estimated when routing did not calculate it.
    public sealed record TravelTime(
        int? Minutes,
        bool IsEstimated,
        double? DistanceKm = null);

    // Route distance in kilometers; null when only travel time could be estimated.
    public sealed record PlaceScoreResult(
        string Nombre,
        string? Prioridad,
        int? Minutos,
        int MaximoMinutos,
        bool Estimado,
        int? Puntaje,
        double? DistanciaKm = null);

    public sealed record ScoreComponent(string Criterion, string Prioridad, int? Puntaje, string Detalle);
    public sealed record LimitResult(string Tipo, bool? Cumple, string Detalle);
    public sealed record ZoneScoreResult(
        ZoneProfile Zona,
        int? Puntaje,
        IReadOnlyList<ScoreComponent> Componentes,
        IReadOnlyList<LimitResult> Limites,
        IReadOnlyList<PlaceScoreResult> Lugares);

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
        public const string Seguridad = "seguridad";
        public const string Movilidad = "movilidad";
        public const string EspaciosVerdes = "espacios-verdes";
        public const string Educacion = "educacion";
        public const string Salud = "salud";
        public const string Lugares = "lugares";

        public const string LimiteAlquiler = "alquiler";
        public const string LimiteExpensas = "expensas";
        public const string LimiteCompra = "compra";
        public const string LimiteTiempo = "tiempo";

        // In a group analysis, the group sets budget limits instead of individual participants.
        public static bool EsLimiteDePresupuesto(string tipo) => tipo is LimiteAlquiler or LimiteExpensas or LimiteCompra;

        // The penalty is linear up to this overage (20%); each additional 20% overage divides the remaining score by e.
        public const double ExcesoLeve = 0.20;
        public const double EscalaExceso = 0.20;

        // Morning peak hours (6 to 9) used to count peak-hour departures.
        private const int HorasHoraPico = 3;

        private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-AR");

        private static readonly IReadOnlyDictionary<string, int> PesoPrioridad = new Dictionary<string, int>
        {
            ["baja"] = 1,
            ["media"] = 3,
            ["alta"] = 9,
        };

        private static readonly IReadOnlyDictionary<string, int> PesoLugar = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Muy importante"] = 4,
            ["Alta"] = 3,
            ["Media"] = 2,
            ["Baja"] = 1,
        };

        // Criterion indicators where higher is better; safety reverses the sign of crime counts.
        private static readonly IReadOnlyDictionary<string, Func<ZoneProfile, double?>> Indicadores =
            new Dictionary<string, Func<ZoneProfile, double?>>
            {
                [Seguridad] = z => -z.DelitosCadaMilHabitantes,
                [Movilidad] = z => z.SalidasHoraPicoPorKm2,
                [EspaciosVerdes] = z => z.VerdePorHabitanteM2,
                [Educacion] = z => z.EscuelasCadaDiezMil,
                [Salud] = z => z.CentrosSaludCadaDiezMil,
            };

        // Sorted indicator values by zone type (large or small), used to calculate percentiles.
        private readonly Dictionary<(string Criterion, bool Grande), double[]> _distribuciones = [];

        public ScoringEngine(IReadOnlyCollection<ZoneProfile> universo)
        {
            foreach (var (criterio, indicador) in Indicadores)
                foreach (var grande in new[] { false, true })
                {
                    _distribuciones[(criterio, grande)] = universo
                        .Where(z => z.IsLargeZone == grande)
                        .Select(indicador)
                        .OfType<double>()
                        .Order()
                        .ToArray();
                }
        }

        public static bool EsCriterioConocido(string criterio) => Indicadores.ContainsKey(criterio);

        // Times are provided for each request place in the same order (null when a place has no location).
        public ZoneScoreResult Calcular(ZoneProfile zona, ScoringRequest pedido, IReadOnlyList<TravelTime?>? tiempos = null)
        {
            var componentes = pedido.Criterios
                .Where(c => Indicadores.ContainsKey(c.Key) && PesoPrioridad.ContainsKey(c.Value))
                .Select(c => Componente(zona, c.Key, c.Value))
                .ToList();

            var lugares = ResultadosLugares(pedido.Lugares, tiempos);
            if (lugares.Count > 0)
                componentes.Add(ComponenteLugares(lugares));

            var limites = new List<LimitResult>();
            var excesoPresupuesto = 0.0;
            var mercado = zona.GetMarket(pedido.TipoVivienda);

            if (pedido.EsCompra && pedido.CompraMaximaUsd is { } compraMaxima && compraMaxima > 0)
            {
                var (limite, exceso) = LimiteDeCompra(mercado, compraMaxima, pedido.TipoVivienda, pedido.SuperficieM2);
                limites.Add(limite);
                excesoPresupuesto = Math.Max(excesoPresupuesto, exceso);
            }

            if (pedido.GastoMensualMaximo is { } maximo && maximo > 0)
            {
                var (limite, exceso) = pedido.EsCompra
                    ? LimiteDeExpensas(mercado, maximo)
                    : LimiteDeAlquiler(mercado, maximo, pedido.TipoVivienda, pedido.SuperficieM2);
                limites.Add(limite);
                excesoPresupuesto = Math.Max(excesoPresupuesto, exceso);
            }

            if (lugares.Count > 0)
                limites.AddRange(LimitesDeTiempo(lugares));

            return new ZoneScoreResult(zona, PuntajeTotal(componentes, excesoPresupuesto), componentes, limites, lugares);
        }

        // Percentile of a value in its distribution (ties count as half), from 0 to 100.
        public static int Percentil(IReadOnlyList<double> ordenados, double valor)
        {
            if (ordenados.Count == 0)
                return 50;

            var menores = 0;
            var iguales = 0;
            foreach (var x in ordenados)
            {
                if (x < valor) menores++;
                else if (x == valor) iguales++;
            }

            return (int)Redondear(100.0 * (menores + 0.5 * iguales) / ordenados.Count);
        }

        // Score after exceeding a limit (0.1 means 10% over): deduct one point per 1% up to ExcesoLeve, then reduce the
        // remaining score exponentially. The score stays unchanged when there is no overage.
        public static double Penalizar(double puntaje, double exceso)
        {
            if (exceso <= 0)
                return puntaje;

            var restante = Math.Max(0, puntaje - Math.Min(exceso, ExcesoLeve) * 100);

            return exceso <= ExcesoLeve ? restante : restante * Math.Exp(-(exceso - ExcesoLeve) / EscalaExceso);
        }

        // Scores range from 100 (arrives immediately) to 80 (exactly at the limit); beyond the limit, apply the usual
        // penalty. Null when travel time could not be calculated.
        public static int? PuntajeViaje(int? minutos, int maximo)
        {
            if (minutos is not { } m)
                return null;

            if (m <= maximo)
                return (int)Redondear(100 - 20.0 * m / maximo);

            return (int)Redondear(Penalizar(80, (double)m / maximo - 1));
        }

        private ScoreComponent Componente(ZoneProfile zona, string criterio, string prioridad)
        {
            var valor = Indicadores[criterio](zona);
            int? puntaje = valor is { } v ? Percentil(_distribuciones[(criterio, zona.IsLargeZone)], v) : null;

            return new ScoreComponent(criterio, prioridad, puntaje, Detalle(zona, criterio));
        }

        private static string Detalle(ZoneProfile zona, string criterio) => criterio switch
        {
            Seguridad => zona.DelitosCadaMilHabitantes is { } delitos
                ? $"{delitos.ToString("N1", Es)} delitos estimados cada 1.000 hab. en un año, incluidos los no denunciados"
                : "Sin datos de delitos",
            Movilidad => $"{zona.LineasTransporte} líneas · {(zona.SalidasHoraPico / HorasHoraPico).ToString("N0", Es)} salidas por hora en hora pico",
            EspaciosVerdes => zona.VerdePorHabitanteM2 is { } verde
                ? $"{verde.ToString("N1", Es)} m² de plazas y parques por habitante"
                : "Sin datos de población para comparar",
            Educacion => $"{zona.Escuelas.ToString("N0", Es)} escuelas y jardines",
            Salud => $"{zona.CentrosSalud.ToString("N0", Es)} hospitales, clínicas y centros de salud",
            _ => string.Empty,
        };

        private static List<PlaceScoreResult> ResultadosLugares(IReadOnlyList<UserPlace> lugares, IReadOnlyList<TravelTime?>? tiempos)
        {
            var resultados = new List<PlaceScoreResult>();

            for (var i = 0; i < lugares.Count; i++)
            {
                if (tiempos is null || i >= tiempos.Count || tiempos[i] is not { } tiempo)
                    continue;

                var lugar = lugares[i];
                resultados.Add(new PlaceScoreResult(
                    lugar.Nombre,
                    lugar.Prioridad,
                    tiempo.Minutes,
                    lugar.TiempoMaximoMinutos,
                    tiempo.IsEstimated,
                    PuntajeViaje(tiempo.Minutes, lugar.TiempoMaximoMinutos),
                    tiempo.DistanceKm));
            }

            return resultados;
        }

        private static ScoreComponent ComponenteLugares(IReadOnlyList<PlaceScoreResult> lugares)
        {
            // Only calculated trips count; places that matter more to the user receive higher weights.
            var medidos = lugares.Where(l => l.Puntaje is not null).ToList();
            if (medidos.Count == 0)
                return new ScoreComponent(Lugares, "alta", null, "No pudimos calcular los viajes a tus lugares");

            var pesos = medidos.Select(l => PesoDeLugar(l.Prioridad)).ToList();
            var puntaje = (int)Redondear(medidos.Select((l, i) => (double)l.Puntaje!.Value * pesos[i]).Sum() / pesos.Sum());
            var aTiempo = medidos.Count(l => l.Minutos <= l.MaximoMinutos);

            return new ScoreComponent(Lugares, "alta", puntaje, $"{aTiempo} de {medidos.Count} lugares dentro de tu tiempo máximo");
        }

        // A place without a known priority uses the medium-priority weight.
        private static int PesoDeLugar(string? prioridad) =>
            prioridad is not null && PesoLugar.TryGetValue(prioridad, out var peso) ? peso : PesoLugar["Media"];

        // Return the limit and the overage (0.1 means 10%); missing data does not affect the score.
        // The limit is monthly, so compare it with rent plus building expenses.
        private static (LimitResult Limite, double Exceso) LimiteDeAlquiler(
            HousingMarket mercado, decimal maximo, string? tipo, int? superficieM2)
        {
            var vivienda = HousingTypes.TextSuffix(tipo);
            if (mercado.MonthlyRentCostForArea(superficieM2) is not { } costo)
                return (new LimitResult(LimiteAlquiler, null, $"Sin datos de alquiler{vivienda} para esta zona"), 0);

            var alquiler = mercado.RentForArea(superficieM2)!.Value;
            var estimado = mercado.EstimatesRent(superficieM2);
            var etiqueta = estimado
                ? $"Alquiler estimado{vivienda} para {superficieM2} m² {Pesos(alquiler)}"
                : $"Alquiler{vivienda} {Pesos(alquiler)}";
            var detallePromedio = mercado.ExpensasPromedio is { } expensas
                ? $"{etiqueta} + expensas {Pesos(expensas)} = {Pesos(costo)} por mes"
                : estimado
                    ? $"{etiqueta} por mes (sin dato de expensas)"
                    : $"Alquiler promedio{vivienda} {Pesos(costo)} (sin dato de expensas)";

            return Comparar(LimiteAlquiler, costo, maximo, detallePromedio, $"tu tope de {Pesos(maximo)}");
        }

        // Buyers do not pay rent; their monthly housing cost is the building expenses.
        private static (LimitResult Limite, double Exceso) LimiteDeExpensas(HousingMarket mercado, decimal maximo)
        {
            if (mercado.ExpensasPromedio is not { } expensas)
                return (new LimitResult(LimiteExpensas, null, "Sin datos de expensas para esta zona"), 0);

            return Comparar(LimiteExpensas, expensas, maximo, $"Expensas promedio {Pesos(expensas)} por mes", $"tu tope mensual de {Pesos(maximo)}");
        }

        private static (LimitResult Limite, double Exceso) LimiteDeCompra(
            HousingMarket mercado, decimal maximo, string? tipo, int? superficieM2)
        {
            var vivienda = HousingTypes.TextSuffix(tipo);
            if (mercado.SalePriceUsdForArea(superficieM2) is not { } precio)
                return (new LimitResult(LimiteCompra, null, $"Sin datos de venta{vivienda} para esta zona"), 0);

            var detalle = mercado.EstimatesSale(superficieM2)
                ? $"Precio estimado{vivienda} para {superficieM2} m² {Dolares(precio)} ({Dolares(mercado.VentaM2Usd!.Value)}/m²)"
                : $"Precio promedio{vivienda} {Dolares(precio)}";

            return Comparar(LimiteCompra, precio, maximo, detalle, $"tu tope de compra de {Dolares(maximo)}");
        }

        private static (LimitResult Limite, double Exceso) Comparar(string tipo, decimal valor, decimal maximo, string detalle, string tope)
        {
            if (valor <= maximo)
                return (new LimitResult(tipo, true, $"{detalle}: dentro de {tope}"), 0);

            var exceso = (double)(valor / maximo - 1);
            var porcentaje = (int)Math.Ceiling(exceso * 100);

            return (new LimitResult(tipo, false, $"{detalle}: supera {tope} en un {porcentaje} %"), exceso);
        }

        private static IEnumerable<LimitResult> LimitesDeTiempo(IReadOnlyList<PlaceScoreResult> lugares)
        {
            var fuera = lugares.Where(l => l.Minutos > l.MaximoMinutos).ToList();
            var sinDato = lugares.Where(l => l.Minutos is null).ToList();

            if (fuera.Count == 0 && sinDato.Count < lugares.Count)
                yield return new LimitResult(LimiteTiempo, true,
                    sinDato.Count == 0 ? "Llegás a todos tus lugares dentro de tu tiempo máximo" : "Llegás a tiempo a los lugares que pudimos medir");

            foreach (var lugar in fuera)
                yield return new LimitResult(LimiteTiempo, false, $"A {lugar.Nombre}: {lugar.Minutos} min (tu máximo es {lugar.MaximoMinutos} min)");

            foreach (var lugar in sinDato)
                yield return new LimitResult(LimiteTiempo, null, $"A {lugar.Nombre}: no pudimos calcular el viaje");
        }

        private static int? PuntajeTotal(IReadOnlyList<ScoreComponent> componentes, double excesoAlquiler)
        {
            var conDato = componentes.Where(c => c.Puntaje is not null).ToList();
            if (conDato.Count == 0)
                return null;

            var pesoTotal = conDato.Sum(c => PesoPrioridad[c.Prioridad]);
            var promedio = conDato.Sum(c => c.Puntaje!.Value * PesoPrioridad[c.Prioridad]) / (double)pesoTotal;

            return (int)Math.Clamp(Redondear(Penalizar(promedio, excesoAlquiler)), 0, 100);
        }

        // Round midpoint values away from zero (12.5 -> 13), instead of to even as Math.Round does by default.
        private static double Redondear(double valor) => Math.Round(valor, MidpointRounding.AwayFromZero);

        private static string Pesos(decimal monto) => $"$ {monto.ToString("N0", Es)}";

        private static string Dolares(decimal monto) => $"US$ {monto.ToString("N0", Es)}";
    }
}
