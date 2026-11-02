using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Application.Interfaces
{
    // Duracion de un viaje entre dos puntos (MOTIS)
    public interface IRoutingClient
    {
        // Minutos de viaje un dia habil a la mañana; Minutos null si no hay recorrido.
        // Null si el servicio de ruteo no esta disponible: el que llama decide si estima.
        Task<TravelTime?> GetTravelTimeAsync(
            double latitudOrigen, double longitudOrigen, double latitudDestino, double longitudDestino, TravelMode modo,
            CancellationToken cancellationToken = default);
    }
}
