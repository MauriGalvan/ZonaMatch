namespace ZonaMatch.Domain.Puntaje
{
    // Lo que el motor de puntaje sabe de una zona: censo, delitos, transporte, equipamiento, alquiler y venta.
    // Lo arma PerfilesZonaRepository para todas las zonas, porque cada criterio compara una zona con las demas.
    public sealed class PerfilZona
    {
        public string Slug { get; init; } = string.Empty;
        public string Nombre { get; init; } = string.Empty;

        // Nivel administrativo de OSM: 5 comunas y partidos, 8 localidades, 9 y 10 barrios
        public int Nivel { get; init; }

        public double SuperficieKm2 { get; init; }
        public int Poblacion { get; init; }

        // Estimated crimes of a year (geo.zona_indicadores.delitos_comparables): registered crimes corrected with the
        // victimization survey, so CABA and the province can be compared
        public int? Delitos { get; init; }

        // Lineas de transporte publico y salidas de la franja de la manana (hora pico) en la zona
        public int LineasTransporte { get; init; }
        public int SalidasHoraPico { get; init; }

        // Escuelas y jardines, centros de salud y plazas o parques (OpenStreetMap)
        public int Escuelas { get; init; }
        public int CentrosSalud { get; init; }
        public int EspaciosVerdes { get; init; }
        public double SuperficieVerdeM2 { get; init; }

        // Centro de la zona (WGS84): origen de los viajes a los lugares del usuario
        public double Latitud { get; init; }
        public double Longitud { get; init; }

        // Alquiler promedio en pesos y propiedades disponibles; null si no hay dato
        public decimal? AlquilerPromedio { get; init; }
        public int? PropiedadesDisponibles { get; init; }

        // Expensas mensuales promedio en pesos; null si no hay dato
        public decimal? ExpensasPromedio { get; init; }

        // Lo que se paga por mes para vivir en la zona: alquiler mas expensas (sin dato de expensas, solo alquiler)
        public decimal? CostoMensual => AlquilerPromedio is { } alquiler ? alquiler + (ExpensasPromedio ?? 0) : null;

        // Precio de venta promedio en dolares y propiedades en venta; null si no hay dato
        public decimal? VentaPromedioUsd { get; init; }
        public int? PropiedadesEnVenta { get; init; }

        // Alquiler en pesos y venta en dolares por m2 (todos los tipos); null si no hay dato
        public decimal? AlquilerM2 { get; init; }
        public decimal? VentaM2Usd { get; init; }

        // Mercado de departamentos, casas y PH por separado (clave: TiposVivienda); los datos de arriba son de todos juntos
        public IReadOnlyDictionary<string, MercadoVivienda> MercadoPorTipo { get; init; } = new Dictionary<string, MercadoVivienda>();

        // Mercado del tipo pedido; sin tipo, el de todos juntos. Casas y PH van sin expensas: la mayoria no paga y
        // las que pagan son de barrios cerrados. Un departamento sin expensas propias usa las de la zona.
        public MercadoVivienda Mercado(string? tipo)
        {
            if (!TiposVivienda.EsValido(tipo))
                return new MercadoVivienda(
                    AlquilerPromedio, PropiedadesDisponibles, ExpensasPromedio, VentaPromedioUsd, PropiedadesEnVenta, AlquilerM2, VentaM2Usd);

            var mercado = MercadoPorTipo.TryGetValue(tipo!, out var propio) ? propio : MercadoVivienda.SinDatos;
            return mercado with { ExpensasPromedio = TiposVivienda.PagaExpensas(tipo) ? mercado.ExpensasPromedio ?? ExpensasPromedio : 0 };
        }

        // Comunas y partidos se comparan entre si, y barrios y localidades entre si
        public bool EsZonaGrande => Nivel <= 5;

        // Indicadores de cada criterio, donde mas es mejor. Null si no hay dato para comparar.

        public double? DelitosCadaMilHabitantes =>
            Delitos is { } delitos && Poblacion > 0 ? delitos * 1000.0 / Poblacion : null;

        public double? EscuelasCadaDiezMil => CadaDiezMil(Escuelas);

        public double? CentrosSaludCadaDiezMil => CadaDiezMil(CentrosSalud);

        public double? VerdePorHabitanteM2 => Poblacion > 0 ? SuperficieVerdeM2 / Poblacion : null;

        public double? SalidasHoraPicoPorKm2 => SuperficieKm2 > 0 ? SalidasHoraPico / SuperficieKm2 : null;

        private double? CadaDiezMil(int cantidad) => Poblacion > 0 ? cantidad * 10000.0 / Poblacion : null;
    }
}
