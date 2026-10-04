using ZonaMatch.Application.DTOs;

namespace ZonaMatch.Application.Interfaces
{
    // Reverse geocoding provider (coordinates -> place name and address)
    public interface IGeocodingClient
    {
        // Returns null when the provider has nothing for that point; throws HttpRequestException when it is unreachable
        Task<DireccionGeocodificadaDto?> ReversaAsync(double latitud, double longitud, CancellationToken cancellationToken = default);
    }
}
