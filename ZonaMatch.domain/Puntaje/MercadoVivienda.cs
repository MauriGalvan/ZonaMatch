namespace ZonaMatch.Domain.Puntaje
{
    // Mercado inmobiliario de una zona para un tipo de vivienda (Zonaprop): alquiler y expensas en pesos, venta en
    // dolares. Null si no hay dato.
    public sealed record MercadoVivienda(
        decimal? AlquilerPromedio,
        int? PropiedadesEnAlquiler,
        decimal? ExpensasPromedio,
        decimal? VentaPromedioUsd,
        int? PropiedadesEnVenta)
    {
        public static readonly MercadoVivienda SinDatos = new(null, null, null, null, null);

        // Lo que se paga por mes alquilando: alquiler mas expensas (sin dato de expensas, solo alquiler)
        public decimal? CostoMensualAlquiler => AlquilerPromedio is { } alquiler ? alquiler + (ExpensasPromedio ?? 0) : null;
    }

    // Lo que busca el usuario: alquilar o comprar
    public static class Operaciones
    {
        public const string Alquiler = "alquiler";
        public const string Compra = "compra";

        public static bool EsValida(string? operacion) => operacion is Alquiler or Compra;
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
