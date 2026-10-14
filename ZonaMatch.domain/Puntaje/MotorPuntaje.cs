using System.Globalization;

namespace ZonaMatch.Domain.Puntaje
{
    // Lo que pidio el usuario: criterios con su prioridad, sus lugares y su tope de alquiler
    public sealed record PedidoPuntaje(
        IReadOnlyDictionary<string, string> Criterios,
        IReadOnlyList<LugarDelUsuario> Lugares,
        decimal? AlquilerMaximo);

    // Un lugar al que el usuario va seguido. Sin coordenadas no se puede medir el viaje.
    public sealed record LugarDelUsuario(string Nombre, double? Latitud, double? Longitud, string? Prioridad, int TiempoMaximoMinutos)
    {
        public bool TieneUbicacion => Latitud is not null && Longitud is not null;
    }

    // Duracion de un viaje; Minutos null si no se llega (o no hay recorrido). Estimado si no lo calculo el ruteo.
    public sealed record TiempoViaje(int? Minutos, bool Estimado);

    public sealed record ComponentePuntaje(string Criterio, string Prioridad, int? Puntaje, string Detalle);

    public sealed record ResultadoLimite(string Tipo, bool? Cumple, string Detalle);

    public sealed record ResultadoLugar(string Nombre, string? Prioridad, int? Minutos, int MaximoMinutos, bool Estimado, int Puntaje);

    public sealed record ResultadoZona(
        PerfilZona Zona,
        int? Puntaje,
        IReadOnlyList<ComponentePuntaje> Componentes,
        IReadOnlyList<ResultadoLimite> Limites,
        IReadOnlyList<ResultadoLugar> Lugares);

    // Calcula el puntaje (0 a 100) de una zona para un pedido. No guarda estado entre calculos: se puede compartir.
    // Cada criterio vale el percentil de la zona entre las de su tipo (barrios y localidades por un lado, comunas y
    // partidos por el otro): 80 quiere decir que la zona esta mejor que el 80 % de las demas en ese criterio.
    // El puntaje es el promedio de los criterios ponderado por la prioridad del usuario (baja 1, media 2, alta 3);
    // los criterios sin dato no cuentan. Los lugares del usuario suman un criterio propio, con prioridad alta,
    // segun cuanto se tarda en llegar. Pasarse del tope de alquiler descuenta hasta 40 puntos.
    public sealed class MotorPuntaje
    {
        public const string Seguridad = "seguridad";
        public const string Movilidad = "movilidad";
        public const string EspaciosVerdes = "espacios-verdes";
        public const string Educacion = "educacion";
        public const string Salud = "salud";
        public const string Lugares = "lugares";

        public const string LimiteAlquiler = "alquiler";
        public const string LimiteTiempo = "tiempo";

        // Lo maximo que descuenta pasarse del tope de alquiler
        public const int DescuentoMaximoAlquiler = 40;

        // Horas de la franja de la manana (6 a 9) en la que se cuentan las salidas de hora pico
        private const int HorasHoraPico = 3;

        private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-AR");

        private static readonly IReadOnlyDictionary<string, int> PesoPrioridad = new Dictionary<string, int>
        {
            ["baja"] = 1,
            ["media"] = 2,
            ["alta"] = 3,
        };

        private static readonly IReadOnlyDictionary<string, int> PesoLugar = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Muy importante"] = 4,
            ["Alta"] = 3,
            ["Media"] = 2,
            ["Baja"] = 1,
        };

        // Indicador de cada criterio (mas es mejor): seguridad compara delitos con el signo invertido
        private static readonly IReadOnlyDictionary<string, Func<PerfilZona, double?>> Indicadores =
            new Dictionary<string, Func<PerfilZona, double?>>
            {
                [Seguridad] = z => -z.DelitosCadaMilHabitantes,
                [Movilidad] = z => z.SalidasHoraPicoPorKm2,
                [EspaciosVerdes] = z => z.VerdePorHabitanteM2,
                [Educacion] = z => z.EscuelasCadaDiezMil,
                [Salud] = z => z.CentrosSaludCadaDiezMil,
            };

        // Valores ordenados de cada indicador, por tipo de zona (grande o no): la base de los percentiles
        private readonly Dictionary<(string Criterio, bool Grande), double[]> _distribuciones = [];

        public MotorPuntaje(IReadOnlyCollection<PerfilZona> universo)
        {
            foreach (var (criterio, indicador) in Indicadores)
                foreach (var grande in new[] { false, true })
                {
                    _distribuciones[(criterio, grande)] = universo
                        .Where(z => z.EsZonaGrande == grande)
                        .Select(indicador)
                        .OfType<double>()
                        .Order()
                        .ToArray();
                }
        }

        public static bool EsCriterioConocido(string criterio) => Indicadores.ContainsKey(criterio);

        // tiempos: uno por lugar del pedido, en el mismo orden (null si el lugar no tiene ubicacion)
        public ResultadoZona Calcular(PerfilZona zona, PedidoPuntaje pedido, IReadOnlyList<TiempoViaje?>? tiempos = null)
        {
            var componentes = pedido.Criterios
                .Where(c => Indicadores.ContainsKey(c.Key) && PesoPrioridad.ContainsKey(c.Value))
                .Select(c => Componente(zona, c.Key, c.Value))
                .ToList();

            var lugares = ResultadosLugares(pedido.Lugares, tiempos);
            if (lugares.Count > 0)
                componentes.Add(ComponenteLugares(lugares));

            var limites = new List<ResultadoLimite>();
            var descuento = 0;

            if (pedido.AlquilerMaximo is { } maximo && maximo > 0)
            {
                var (limite, descuentoAlquiler) = LimiteDeAlquiler(zona, maximo);
                limites.Add(limite);
                descuento = descuentoAlquiler;
            }

            if (lugares.Count > 0)
                limites.AddRange(LimitesDeTiempo(lugares));

            return new ResultadoZona(zona, PuntajeTotal(componentes, descuento), componentes, limites, lugares);
        }

        // Percentil del valor entre los de la distribucion (los empates cuentan la mitad), de 0 a 100
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

        // De 100 (llega enseguida) a 80 (justo en el maximo); despues baja hasta 0 al doble del maximo
        public static int PuntajeViaje(int? minutos, int maximo)
        {
            if (minutos is not { } m)
                return 0;

            if (m <= maximo)
                return (int)Redondear(100 - 20.0 * m / maximo);

            return (int)Redondear(Math.Max(0, 80.0 * (2 * maximo - m) / maximo));
        }

        private ComponentePuntaje Componente(PerfilZona zona, string criterio, string prioridad)
        {
            var valor = Indicadores[criterio](zona);
            int? puntaje = valor is { } v ? Percentil(_distribuciones[(criterio, zona.EsZonaGrande)], v) : null;

            return new ComponentePuntaje(criterio, prioridad, puntaje, Detalle(zona, criterio));
        }

        private static string Detalle(PerfilZona zona, string criterio) => criterio switch
        {
            Seguridad => zona.DelitosCadaMilHabitantes is { } delitos
                ? $"{delitos.ToString("N1", Es)} delitos cada 1.000 hab. en un año"
                : "Sin datos de delitos (por ahora solo hay datos de CABA)",
            Movilidad => $"{zona.LineasTransporte} líneas · {(zona.SalidasHoraPico / HorasHoraPico).ToString("N0", Es)} salidas por hora en hora pico",
            EspaciosVerdes => zona.VerdePorHabitanteM2 is { } verde
                ? $"{verde.ToString("N1", Es)} m² de plazas y parques por habitante"
                : "Sin datos de población para comparar",
            Educacion => $"{zona.Escuelas.ToString("N0", Es)} escuelas y jardines",
            Salud => $"{zona.CentrosSalud.ToString("N0", Es)} hospitales, clínicas y centros de salud",
            _ => string.Empty,
        };

        private static List<ResultadoLugar> ResultadosLugares(IReadOnlyList<LugarDelUsuario> lugares, IReadOnlyList<TiempoViaje?>? tiempos)
        {
            var resultados = new List<ResultadoLugar>();

            for (var i = 0; i < lugares.Count; i++)
            {
                if (tiempos is null || i >= tiempos.Count || tiempos[i] is not { } tiempo)
                    continue;

                var lugar = lugares[i];
                resultados.Add(new ResultadoLugar(
                    lugar.Nombre, lugar.Prioridad, tiempo.Minutos, lugar.TiempoMaximoMinutos, tiempo.Estimado,
                    PuntajeViaje(tiempo.Minutos, lugar.TiempoMaximoMinutos)));
            }

            return resultados;
        }

        private static ComponentePuntaje ComponenteLugares(IReadOnlyList<ResultadoLugar> lugares)
        {
            // Pesa mas el lugar mas importante para el usuario
            var pesos = lugares.Select(l => PesoDeLugar(l.Prioridad)).ToList();
            var puntaje = (int)Redondear(lugares.Select((l, i) => (double)l.Puntaje * pesos[i]).Sum() / pesos.Sum());
            var aTiempo = lugares.Count(l => l.Minutos is { } m && m <= l.MaximoMinutos);

            return new ComponentePuntaje(Lugares, "alta", puntaje, $"{aTiempo} de {lugares.Count} lugares dentro de tu tiempo máximo");
        }

        // Sin prioridad (o una desconocida), el lugar pesa como uno de prioridad media
        private static int PesoDeLugar(string? prioridad) =>
            prioridad is not null && PesoLugar.TryGetValue(prioridad, out var peso) ? peso : PesoLugar["Media"];

        private static (ResultadoLimite Limite, int Descuento) LimiteDeAlquiler(PerfilZona zona, decimal maximo)
        {
            if (zona.AlquilerPromedio is not { } promedio)
                return (new ResultadoLimite(LimiteAlquiler, null, "Sin datos de alquiler para esta zona"), 0);

            var detallePromedio = $"Alquiler promedio {Pesos(promedio)}";

            if (promedio <= maximo)
                return (new ResultadoLimite(LimiteAlquiler, true, $"{detallePromedio}: dentro de tu tope de {Pesos(maximo)}"), 0);

            // Un punto por cada 1 % que se pasa, hasta el maximo
            var exceso = (double)(promedio / maximo - 1);
            var descuento = (int)Math.Min(DescuentoMaximoAlquiler, Math.Ceiling(exceso * 100));

            return (new ResultadoLimite(LimiteAlquiler, false, $"{detallePromedio}: supera tu tope de {Pesos(maximo)}"), descuento);
        }

        private static IEnumerable<ResultadoLimite> LimitesDeTiempo(IReadOnlyList<ResultadoLugar> lugares)
        {
            var fuera = lugares.Where(l => l.Minutos is not { } m || m > l.MaximoMinutos).ToList();

            if (fuera.Count == 0)
            {
                yield return new ResultadoLimite(LimiteTiempo, true, "Llegás a todos tus lugares dentro de tu tiempo máximo");
                yield break;
            }

            foreach (var lugar in fuera)
            {
                var duracion = lugar.Minutos is { } m ? $"{m} min" : "más de 2 h";
                yield return new ResultadoLimite(LimiteTiempo, false, $"A {lugar.Nombre}: {duracion} (tu máximo es {lugar.MaximoMinutos} min)");
            }
        }

        private static int? PuntajeTotal(IReadOnlyList<ComponentePuntaje> componentes, int descuento)
        {
            var conDato = componentes.Where(c => c.Puntaje is not null).ToList();
            if (conDato.Count == 0)
                return null;

            var pesoTotal = conDato.Sum(c => PesoPrioridad[c.Prioridad]);
            var promedio = conDato.Sum(c => c.Puntaje!.Value * PesoPrioridad[c.Prioridad]) / (double)pesoTotal;

            return (int)Math.Clamp(Redondear(promedio) - descuento, 0, 100);
        }

        // Los puntajes redondean el medio hacia arriba (12,5 -> 13), no al par como Math.Round por defecto
        private static double Redondear(double valor) => Math.Round(valor, MidpointRounding.AwayFromZero);

        private static string Pesos(decimal monto) => $"$ {monto.ToString("N0", Es)}";
    }
}
