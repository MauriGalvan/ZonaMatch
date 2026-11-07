namespace ZonaMatch.Application.UseCases.Scoring
{
    // How many zones an analysis can add or compare, and how many starting points it can choose inside them.
    public static class ScoringZoneLimits
    {
        // Zones added by hand after the automatic ranking
        public const int AddedZones = 5;

        // Starting points chosen inside a zone instead of its center
        public const int StartingPoints = 10;

        // Zones compared in a saved group analysis
        public const int GroupComparedZones = 10;
    }
}
