namespace ZonaMatch.Domain.Common
{
    // Franjas de un dia habil en las que se resumen las salidas del transporte publico (etl/procesar_gtfs.py
    // usa los mismos codigos y horarios). Desde inclusive, Hasta exclusive, en horas.
    public sealed record FranjaHoraria(string Codigo, int Desde, int Hasta)
    {
        public static readonly FranjaHoraria Madrugada = new("madrugada", 0, 6);
        public static readonly FranjaHoraria Manana = new("manana", 6, 9);
        public static readonly FranjaHoraria Mediodia = new("mediodia", 9, 16);
        public static readonly FranjaHoraria Tarde = new("tarde", 16, 20);
        public static readonly FranjaHoraria Noche = new("noche", 20, 24);

        public static readonly IReadOnlyList<FranjaHoraria> Todas = [Madrugada, Manana, Mediodia, Tarde, Noche];

        public int Horas => Hasta - Desde;

        // Salidas promedio por hora en la franja, con un decimal
        public double SalidasPorHora(int salidas) => Math.Round((double)salidas / Horas, 1);

        // Cada cuantos minutos sale, en promedio. Null si no hay servicio.
        public int? MinutosEntreSalidas(int salidas) => salidas > 0 ? (int)Math.Round(60.0 * Horas / salidas) : null;

        public static FranjaHoraria? PorCodigo(string codigo) => Todas.FirstOrDefault(f => f.Codigo == codigo);
    }
}
