using ZonaMatch.Domain.Puntaje;

namespace ZonaMatch.Application.Interfaces
{
    // Duracion de un viaje entre dos puntos (MOTIS)
    public interface IRuteoClient
    {
        // Minutos de viaje un dia habil a la mañana; Minutos null si no hay recorrido.
        // Null si el servicio de ruteo no esta disponible: el que llama decide si estima.
        Task<TiempoViaje?> CalcularAsync(
            double latitudOrigen, double longitudOrigen, double latitudDestino, double longitudDestino, ModoViaje modo,
            CancellationToken cancellationToken = default);
    }
}
