namespace ZonaMatch.Domain.Entities
{
    // A zone compared in an analysis (app.analisis_zonas table). Zones are referenced by slug,
    // as they are in community features; they are not stored in an application table.
    public class AnalysisZone
    {
        public Guid AnalisisId { get; set; }

        // Position in the user's list (1, 2, ...).
        public int Orden { get; set; }

        public string ZonaSlug { get; set; } = string.Empty;

        // Name displayed when the user selected it ("Villa Luro, CABA"), so it can be listed without querying zones.
        public string Nombre { get; set; } = string.Empty;

        // Starting zone selected in the wizard.
        public bool EsInicial { get; set; }

        // Zone manually added to the automatic ranking.
        public bool EsAgregada { get; set; }

        // Starting point the user marked inside the zone; null = the center of the zone.
        // Trips to the places are measured from here, so it changes the score.
        public double? OrigenLatitud { get; set; }
        public double? OrigenLongitud { get; set; }

        public bool TieneOrigen => OrigenLatitud.HasValue && OrigenLongitud.HasValue;

        // Same zone for another analysis (a version or a recalculation)
        public AnalysisZone Copiar() =>
            new()
            {
                Orden = Orden,
                ZonaSlug = ZonaSlug,
                Nombre = Nombre,
                EsInicial = EsInicial,
                EsAgregada = EsAgregada,
                OrigenLatitud = OrigenLatitud,
                OrigenLongitud = OrigenLongitud,
            };
    }
}
