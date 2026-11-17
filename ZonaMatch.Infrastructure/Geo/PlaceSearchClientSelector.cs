using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Infrastructure.Geo
{
    // The configured provider answers every request; "provider" in the query only wins when overrides are allowed.
    public sealed class PlaceSearchClientSelector(IServiceProvider services, string defaultProvider, bool allowOverride)
        : IPlaceSearchClientSelector
    {
        public IPlaceSearchClient Resolve(string? provider)
        {
            var name = allowOverride && !string.IsNullOrWhiteSpace(provider) ? provider.Trim() : defaultProvider;

            var clientType = name.ToLowerInvariant() switch
            {
                "nominatim" => typeof(NominatimPlaceSearchClient),
                "photon" => typeof(PhotonPlaceSearchClient),
                _ => throw new BadRequestException($"Proveedor de búsqueda desconocido: \"{name}\". Usá \"nominatim\" o \"photon\".")
            };

            return (IPlaceSearchClient)services.GetService(clientType)!;
        }
    }
}
