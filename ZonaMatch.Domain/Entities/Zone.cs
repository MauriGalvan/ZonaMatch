namespace ZonaMatch.Domain.Entities
{
    // A zone: an OpenStreetMap administrative boundary (neighborhood, locality, commune, or district) chosen in
    // geo.zonas by etl/calculate_zones.sql. Its slug is kept for each OSM id across imports (geo.zona_slugs), so
    // reviews, questions, contributions, and links still point to it if OSM renames the place.
    // The rules that need the geometry (levels, places that share a name, jurisdictions) stay in PostGIS.
    public sealed class Zone
    {
        public long OsmId { get; init; }
        public string Slug { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;

        // OSM admin_level: 4 for CABA, 5 for communes and districts, 8 for localities, and 9 or 10 for neighborhoods.
        public int AdministrativeLevel { get; init; }

        public double AreaKm2 { get; init; }

        // The boundaries that contain the zone, most specific first.
        public IReadOnlyList<ZoneJurisdiction> Jurisdictions { get; init; } = [];

        // Only present when the zone is loaded to be drawn (search results do not need it).
        public ZoneBoundary? Boundary { get; init; }

        // OSM feature id: osm2pgsql stores relations with negative ids ("r" prefix) and ways with positive ones ("w").
        public string FeatureId => OsmId < 0 ? $"r{-OsmId}" : $"w{OsmId}";
    }
}
