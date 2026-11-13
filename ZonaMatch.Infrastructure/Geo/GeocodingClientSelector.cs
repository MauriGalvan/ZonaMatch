using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Infrastructure.Geo
{
    // The configured provider answers every request; "provider" in the query only wins when overrides are allowed.
    public sealed class GeocodingClientSelector(IServiceProvider services, string defaultProvider, bool allowOverride)
        : IGeocodingClientSelector
    {
        public IGeocodingClient Resolve(string? provider)
        {
            var name = allowOverride && !string.IsNullOrWhiteSpace(provider) ? provider.Trim() : defaultProvider;

            var clientType = name.ToLowerInvariant() switch
            {
                "osm" => typeof(OsmGeocodingClient),
                "nominatim" => typeof(NominatimGeocodingClient),
                "photon" => typeof(PhotonGeocodingClient),
                _ => throw new BadRequestException($"Proveedor de geocodificación desconocido: \"{name}\". Usá \"osm\", \"nominatim\" o \"photon\".")
            };

            return (IGeocodingClient)services.GetService(clientType)!;
        }
    }
}
