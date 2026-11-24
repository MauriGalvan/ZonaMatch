using System.Text.Json;
using ZonaMatch.Application.DTOs;

namespace ZonaMatch.Infrastructure.Geo;

public sealed record PlaceSearchBounds(
    double MinLongitude,
    double MinLatitude,
    double MaxLongitude,
    double MaxLatitude)
{
    private const string ResourceName = "ZonaMatch.AmbaBounds.json";

    public static PlaceSearchBounds LoadAmba()
    {
        using var stream = typeof(PlaceSearchBounds).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The shared AMBA bounds file is missing.");
        var bounds = JsonSerializer.Deserialize<PlaceSearchBounds>(
            stream, new JsonSerializerOptions { RespectRequiredConstructorParameters = true });

        if (bounds is null || !new BoundingBox(
                bounds.MinLongitude, bounds.MinLatitude, bounds.MaxLongitude, bounds.MaxLatitude).EsValido)
            throw new InvalidOperationException("The shared AMBA bounds are invalid.");

        return bounds;
    }
}
