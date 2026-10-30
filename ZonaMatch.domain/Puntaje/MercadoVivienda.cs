namespace ZonaMatch.Domain.Puntaje
{
    // Mercado inmobiliario de una zona para un tipo de vivienda (Zonaprop): alquiler y expensas en pesos, venta en
    // dolares, tambien por m2. Null si no hay dato. AmbientesAlquiler y AmbientesVenta: los ambientes de los precios
    // (1 a 5, 5 = 5 o mas); null si son de todos los tamanos.
    public sealed record MercadoVivienda(
        decimal? AlquilerPromedio,
        int? PropiedadesEnAlquiler,
        decimal? ExpensasPromedio,
        decimal? VentaPromedioUsd,
        int? PropiedadesEnVenta,
        decimal? AlquilerM2 = null,
        decimal? VentaM2Usd = null,
        int? AmbientesAlquiler = null,
        int? AmbientesVenta = null)
    {
        public static readonly MercadoVivienda SinDatos = new(null, null, null, null, null);

        // Lo que se paga por mes alquilando: alquiler mas expensas (sin dato de expensas, solo alquiler)
        public decimal? CostoMensualAlquiler => AlquilerPromedio is { } alquiler ? alquiler + (ExpensasPromedio ?? 0) : null;

        // Con superficie y precio por m2, el precio es el de esa superficie; si no, el promedio de la zona.
        // El promedio mezcla todos los tamanos: un 2 ambientes chico sale bastante menos.
        public bool EstimaAlquiler(int? superficieM2) => superficieM2 is > 0 && AlquilerM2 is not null;

        public bool EstimaVenta(int? superficieM2) => superficieM2 is > 0 && VentaM2Usd is not null;

        public decimal? AlquilerPara(int? superficieM2) =>
            EstimaAlquiler(superficieM2) ? Math.Round(AlquilerM2!.Value * superficieM2!.Value) : AlquilerPromedio;

        public decimal? VentaParaUsd(int? superficieM2) =>
            EstimaVenta(superficieM2) ? Math.Round(VentaM2Usd!.Value * superficieM2!.Value) : VentaPromedioUsd;

        public decimal? CostoMensualAlquilerPara(int? superficieM2) =>
            AlquilerPara(superficieM2) is { } alquiler ? alquiler + (ExpensasPromedio ?? 0) : null;
    }

    // Lo que busca el usuario: alquilar o comprar
    public static class Operaciones
    {
        public const string Alquiler = "alquiler";
        public const string Compra = "compra";

        public static bool EsValida(string? operacion) => operacion is Alquiler or Compra;
    }

    // Cantidad de ambientes que busca el usuario, con los grupos de app.zone_market_rooms: 1 a 4, y 5 para 5 o mas
    public static class Ambientes
    {
        public const int Minimo = 1;
        public const int Maximo = 5;

        public static bool EsValido(int ambientes) => ambientes is >= Minimo and <= Maximo;

        // Para los textos: " de 2 ambientes"; vacio sin ambientes
        public static string Sufijo(int? ambientes) => ambientes switch
        {
            null => string.Empty,
            1 => " de 1 ambiente",
            >= Maximo => $" de {Maximo} ambientes o más",
            _ => $" de {ambientes} ambientes",
        };
    }

    // Tipos de vivienda, con los mismos valores que app.zone_market.property_type ("all" son todos juntos)
    public static class TiposVivienda
    {
        public const string Departamento = "apartment";
        public const string Casa = "house";
        public const string Ph = "ph";
        public const string Todos = "all";

        public static bool EsValido(string? tipo) => tipo is Departamento or Casa or Ph;

        // Para los textos: " de departamentos"; vacio sin tipo
        public static string Sufijo(string? tipo) => tipo switch
        {
            Departamento => " de departamentos",
            Casa => " de casas",
            Ph => " de PH",
            _ => string.Empty,
        };
    }
}
