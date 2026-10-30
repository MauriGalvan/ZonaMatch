using System.Globalization;

namespace ZonaMatch.Domain.Puntaje
{
    // Lo que pidio el usuario: criterios con su prioridad, sus lugares y sus topes.
    // GastoMensualMaximo es en pesos: alquiler mas expensas si alquila, solo expensas si compra.
    // CompraMaximaUsd es el precio maximo de compra en dolares (solo si compra). Sin TipoVivienda, todos los tipos.
    // Con SuperficieM2, los precios de cada zona se estiman por m2 para esa superficie.
    public sealed record PedidoPuntaje(
        IReadOnlyDictionary<string, string> Criterios,
        IReadOnlyList<LugarDelUsuario> Lugares,
        decimal? GastoMensualMaximo,
        string Operacion = Operaciones.Alquiler,
        decimal? CompraMaximaUsd = null,
        string? TipoVivienda = null,
        int? SuperficieM2 = null)
    {
        public bool EsCompra => Operacion == Operaciones.Compra;
    }

    // Un lugar al que el usuario va seguido. Sin coordenadas no se puede medir el viaje.
    public sealed record LugarDelUsuario(string Nombre, double? Latitud, double? Longitud, string? Prioridad, int TiempoMaximoMinutos)
    {
        public bool TieneUbicacion => Latitud is not null && Longitud is not null;
    }

    // Duracion de un viaje; Minutos null si no se llega (o no hay recorrido). Estimado si no lo calculo el ruteo.
    public sealed record TiempoViaje(
        int? Minutos,
        bool Estimado,
        double? DistanciaKm = null);

    // Distancia del itinerario en km; null si solo se pudieron estimar los minutos.
    public sealed record ResultadoLugar(
        string Nombre,
        string? Prioridad,
        int? Minutos,
        int MaximoMinutos,
        bool Estimado,
        int? Puntaje,
        double? DistanciaKm = null);

    public sealed record ComponentePuntaje(string Criterio, string Prioridad, int? Puntaje, string Detalle);
    public sealed record ResultadoLimite(string Tipo, bool? Cumple, string Detalle);
    public sealed record ResultadoZona(
        PerfilZona Zona,
        int? Puntaje,
        IReadOnlyList<ComponentePuntaje> Componentes,
        IReadOnlyList<ResultadoLimite> Limites,
        IReadOnlyList<ResultadoLugar> Lugares);

    // Calcula el puntaje (0 a 100) de una zona para un pedido. No guarda estado entre calculos: se puede compartir.
    // Cada criterio vale el percentil de la zona entre las de su tipo (barrios y localidades por un lado, comunas y
    // partidos por el otro): 80 quiere decir que la zona esta mejor que el 80 % de las demas en ese criterio.
    // El puntaje es el promedio de los criterios ponderado por la prioridad del usuario (baja 1, media 3, alta 9: cada
    // nivel pesa el triple que el anterior, asi un criterio en baja casi no mueve el puntaje y uno en alta lo define);
    // los criterios sin dato (por ejemplo, seguridad fuera de CABA) no cuentan: ni suman ni restan. Los lugares del
    // usuario suman un criterio propio, con prioridad alta, segun cuanto se tarda en llegar.
    // Limites (topes de plata y tiempo maximo a cada lugar): pasarse un poco (hasta ExcesoLeve) descuenta un punto
    // por cada 1 %; pasarse mucho hace caer exponencialmente lo que queda. Un limite sin dato no suma ni resta.
    public sealed class MotorPuntaje
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

        // Limites de plata: en un grupo los pone el grupo, no cada participante
        public static bool EsLimiteDePresupuesto(string tipo) => tipo is LimiteAlquiler or LimiteExpensas or LimiteCompra;

        // Hasta cuanto exceso (20 %) el descuento es lineal; despues, cada EscalaExceso mas divide lo que queda por e
        public const double ExcesoLeve = 0.20;
        public const double EscalaExceso = 0.20;

        // Horas de la franja de la manana (6 a 9) en la que se cuentan las salidas de hora pico
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
            var excesoPresupuesto = 0.0;
            var mercado = zona.Mercado(pedido.TipoVivienda);

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

            return new ResultadoZona(zona, PuntajeTotal(componentes, excesoPresupuesto), componentes, limites, lugares);
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

        // Puntaje tras pasarse un limite por "exceso" (0,1 = 10 % por encima): un punto menos por cada 1 % hasta
        // ExcesoLeve; despues, lo que queda cae exponencialmente. Sin exceso, el puntaje no cambia.
        public static double Penalizar(double puntaje, double exceso)
        {
            if (exceso <= 0)
                return puntaje;

            var restante = Math.Max(0, puntaje - Math.Min(exceso, ExcesoLeve) * 100);

            return exceso <= ExcesoLeve ? restante : restante * Math.Exp(-(exceso - ExcesoLeve) / EscalaExceso);
        }

        // De 100 (llega enseguida) a 80 (justo en el maximo); pasado el maximo, se penaliza como cualquier limite.
        // Null si no se pudo calcular el viaje.
        public static int? PuntajeViaje(int? minutos, int maximo)
        {
            if (minutos is not { } m)
                return null;

            if (m <= maximo)
                return (int)Redondear(100 - 20.0 * m / maximo);

            return (int)Redondear(Penalizar(80, (double)m / maximo - 1));
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

        private static List<ResultadoLugar> ResultadosLugares(IReadOnlyList<LugarDelUsuario> lugares, IReadOnlyList<TiempoViaje?>? tiempos)
        {
            var resultados = new List<ResultadoLugar>();

            for (var i = 0; i < lugares.Count; i++)
            {
                if (tiempos is null || i >= tiempos.Count || tiempos[i] is not { } tiempo)
                    continue;

                var lugar = lugares[i];
                resultados.Add(new ResultadoLugar(
                    lugar.Nombre,
                    lugar.Prioridad,
                    tiempo.Minutos,
                    lugar.TiempoMaximoMinutos,
                    tiempo.Estimado,
                    PuntajeViaje(tiempo.Minutos, lugar.TiempoMaximoMinutos),
                    tiempo.DistanciaKm));
            }

            return resultados;
        }

        private static ComponentePuntaje ComponenteLugares(IReadOnlyList<ResultadoLugar> lugares)
        {
            // Solo cuentan los viajes calculados; pesa mas el lugar mas importante para el usuario
            var medidos = lugares.Where(l => l.Puntaje is not null).ToList();
            if (medidos.Count == 0)
                return new ComponentePuntaje(Lugares, "alta", null, "No pudimos calcular los viajes a tus lugares");

            var pesos = medidos.Select(l => PesoDeLugar(l.Prioridad)).ToList();
            var puntaje = (int)Redondear(medidos.Select((l, i) => (double)l.Puntaje!.Value * pesos[i]).Sum() / pesos.Sum());
            var aTiempo = medidos.Count(l => l.Minutos <= l.MaximoMinutos);

            return new ComponentePuntaje(Lugares, "alta", puntaje, $"{aTiempo} de {medidos.Count} lugares dentro de tu tiempo máximo");
        }

        // Sin prioridad (o una desconocida), el lugar pesa como uno de prioridad media
        private static int PesoDeLugar(string? prioridad) =>
            prioridad is not null && PesoLugar.TryGetValue(prioridad, out var peso) ? peso : PesoLugar["Media"];

        // El limite y cuanto se pasa del tope (0,1 = 10 %); sin dato, ni suma ni resta.
        // El tope es mensual, asi que se compara con alquiler mas expensas.
        private static (ResultadoLimite Limite, double Exceso) LimiteDeAlquiler(
            MercadoVivienda mercado, decimal maximo, string? tipo, int? superficieM2)
        {
            var vivienda = TiposVivienda.Sufijo(tipo);
            if (mercado.CostoMensualAlquilerPara(superficieM2) is not { } costo)
                return (new ResultadoLimite(LimiteAlquiler, null, $"Sin datos de alquiler{vivienda} para esta zona"), 0);

            var alquiler = mercado.AlquilerPara(superficieM2)!.Value;
            var estimado = mercado.EstimaAlquiler(superficieM2);
            var etiqueta = estimado
                ? $"Alquiler estimado{vivienda} para {superficieM2} m² {Pesos(alquiler)}"
                : $"Alquiler{vivienda} {Pesos(alquiler)}";
            var detallePromedio = mercado.ExpensasPromedio is { } expensas
                ? expensas == 0
                    ? $"{etiqueta} por mes (sin expensas)"
                    : $"{etiqueta} + expensas {Pesos(expensas)} = {Pesos(costo)} por mes"
                : estimado
                    ? $"{etiqueta} por mes (sin dato de expensas)"
                    : $"Alquiler promedio{vivienda} {Pesos(costo)} (sin dato de expensas)";

            return Comparar(LimiteAlquiler, costo, maximo, detallePromedio, $"tu tope de {Pesos(maximo)}");
        }

        // Quien compra no paga alquiler: su gasto mensual son las expensas
        private static (ResultadoLimite Limite, double Exceso) LimiteDeExpensas(MercadoVivienda mercado, decimal maximo)
        {
            if (mercado.ExpensasPromedio is not { } expensas)
                return (new ResultadoLimite(LimiteExpensas, null, "Sin datos de expensas para esta zona"), 0);

            if (expensas == 0)
                return (new ResultadoLimite(LimiteExpensas, true, "Casas y PH se toman sin expensas: dentro de tu tope mensual"), 0);

            return Comparar(LimiteExpensas, expensas, maximo, $"Expensas promedio {Pesos(expensas)} por mes", $"tu tope mensual de {Pesos(maximo)}");
        }

        private static (ResultadoLimite Limite, double Exceso) LimiteDeCompra(
            MercadoVivienda mercado, decimal maximo, string? tipo, int? superficieM2)
        {
            var vivienda = TiposVivienda.Sufijo(tipo);
            if (mercado.VentaParaUsd(superficieM2) is not { } precio)
                return (new ResultadoLimite(LimiteCompra, null, $"Sin datos de venta{vivienda} para esta zona"), 0);

            var detalle = mercado.EstimaVenta(superficieM2)
                ? $"Precio estimado{vivienda} para {superficieM2} m² {Dolares(precio)} ({Dolares(mercado.VentaM2Usd!.Value)}/m²)"
                : $"Precio promedio{vivienda} {Dolares(precio)}";

            return Comparar(LimiteCompra, precio, maximo, detalle, $"tu tope de compra de {Dolares(maximo)}");
        }

        private static (ResultadoLimite Limite, double Exceso) Comparar(string tipo, decimal valor, decimal maximo, string detalle, string tope)
        {
            if (valor <= maximo)
                return (new ResultadoLimite(tipo, true, $"{detalle}: dentro de {tope}"), 0);

            var exceso = (double)(valor / maximo - 1);
            var porcentaje = (int)Math.Ceiling(exceso * 100);

            return (new ResultadoLimite(tipo, false, $"{detalle}: supera {tope} en un {porcentaje} %"), exceso);
        }

        private static IEnumerable<ResultadoLimite> LimitesDeTiempo(IReadOnlyList<ResultadoLugar> lugares)
        {
            var fuera = lugares.Where(l => l.Minutos > l.MaximoMinutos).ToList();
            var sinDato = lugares.Where(l => l.Minutos is null).ToList();

            if (fuera.Count == 0 && sinDato.Count < lugares.Count)
                yield return new ResultadoLimite(LimiteTiempo, true,
                    sinDato.Count == 0 ? "Llegás a todos tus lugares dentro de tu tiempo máximo" : "Llegás a tiempo a los lugares que pudimos medir");

            foreach (var lugar in fuera)
                yield return new ResultadoLimite(LimiteTiempo, false, $"A {lugar.Nombre}: {lugar.Minutos} min (tu máximo es {lugar.MaximoMinutos} min)");

            foreach (var lugar in sinDato)
                yield return new ResultadoLimite(LimiteTiempo, null, $"A {lugar.Nombre}: no pudimos calcular el viaje");
        }

        private static int? PuntajeTotal(IReadOnlyList<ComponentePuntaje> componentes, double excesoAlquiler)
        {
            var conDato = componentes.Where(c => c.Puntaje is not null).ToList();
            if (conDato.Count == 0)
                return null;

            var pesoTotal = conDato.Sum(c => PesoPrioridad[c.Prioridad]);
            var promedio = conDato.Sum(c => c.Puntaje!.Value * PesoPrioridad[c.Prioridad]) / (double)pesoTotal;

            return (int)Math.Clamp(Redondear(Penalizar(promedio, excesoAlquiler)), 0, 100);
        }

        // Los puntajes redondean el medio hacia arriba (12,5 -> 13), no al par como Math.Round por defecto
        private static double Redondear(double valor) => Math.Round(valor, MidpointRounding.AwayFromZero);

        private static string Pesos(decimal monto) => $"$ {monto.ToString("N0", Es)}";

        private static string Dolares(decimal monto) => $"US$ {monto.ToString("N0", Es)}";
    }
}
