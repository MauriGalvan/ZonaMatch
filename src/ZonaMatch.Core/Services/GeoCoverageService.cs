namespace ZonaMatch.Core.Services;

public class GeoCoverageService : IGeoCoverageService
{
    // Bounding Box para CABA y los 40 municipios del Gran Buenos Aires (AMBA)
    // Suroeste: Cañuelas / Marcos Paz / La Plata (-35.15, -59.35)
    // Noreste: Zárate / Campana / Tigre / Costanera (-34.15, -57.80)
    public const double SouthWestLat = -35.15;
    public const double SouthWestLon = -59.35;
    public const double NorthEastLat = -34.15;
    public const double NorthEastLon = -57.80;

    public const double CenterLat = -34.6037;
    public const double CenterLon = -58.3816;

    public bool IsInsideCoverage(double latitude, double longitude)
    {
        return latitude >= SouthWestLat && latitude <= NorthEastLat &&
               longitude >= SouthWestLon && longitude <= NorthEastLon;
    }

    public CoverageInfo GetCoverageInfo()
    {
        return new CoverageInfo(
            RegionName: "CABA y AMBA (Área Metropolitana de Buenos Aires)",
            SouthWestLat: SouthWestLat,
            SouthWestLon: SouthWestLon,
            NorthEastLat: NorthEastLat,
            NorthEastLon: NorthEastLon,
            CenterLat: CenterLat,
            CenterLon: CenterLon,
            MinZoom: 10,
            DefaultZoom: 13,
            Presets: new List<PresetZoneInfo>
            {
                new("Palermo", "CABA Centro-Norte", -34.5889, -58.4306),
                new("Caballito", "CABA Centro", -34.6201, -58.4443),
                new("Belgrano", "CABA Norte", -34.5627, -58.4564),
                new("Vicente López", "AMBA Norte", -34.5298, -58.4736),
                new("San Isidro", "AMBA Norte", -34.4718, -58.5286),
                new("Ramos Mejía", "AMBA Oeste (La Matanza)", -34.6469, -58.5639),
                new("Quilmes Centro", "AMBA Sur", -34.7242, -58.2608),
                new("Lanús Centro", "AMBA Sur", -34.7072, -58.3934)
            }
        );
    }
}
