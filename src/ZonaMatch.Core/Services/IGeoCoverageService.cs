namespace ZonaMatch.Core.Services;

public interface IGeoCoverageService
{
    bool IsInsideCoverage(double latitude, double longitude);
    CoverageInfo GetCoverageInfo();
}

public record CoverageInfo(
    string RegionName,
    double SouthWestLat,
    double SouthWestLon,
    double NorthEastLat,
    double NorthEastLon,
    double CenterLat,
    double CenterLon,
    int MinZoom,
    int DefaultZoom,
    List<PresetZoneInfo> Presets
);

public record PresetZoneInfo(string Name, string Subtitle, double Lat, double Lon);
