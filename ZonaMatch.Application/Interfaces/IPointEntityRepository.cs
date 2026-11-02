using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.Interfaces
{
    // Repositorio para entidades geo-localizadas mediante un punto (latitud/longitud)
    public interface IPointEntityRepository<T> : IRepository<T> where T : class, IPointLocation
    {
        // Entidades dentro de un radio (en metros) desde el punto dado, ordenadas por distancia ascendente
        Task<IReadOnlyList<(T Entidad, double DistanciaMetros)>> GetNearbyAsync(double latitud, double longitud, double radioMetros);

        // Las N entidades mas cercanas al punto dado (sin limite de radio), ordenadas por distancia ascendente
        Task<IReadOnlyList<(T Entidad, double DistanciaMetros)>> GetNearestAsync(double latitud, double longitud, int cantidad);
    }
}
