using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.Interfaces
{
    // Repositorio para entidades geo-localizadas mediante un punto (latitud/longitud)
    public interface IGeoPuntoRepository<T> : IRepository<T> where T : class, IUbicacionPunto
    {
        // Entidades dentro de un radio (en metros) desde el punto dado, ordenadas por distancia ascendente
        Task<IReadOnlyList<(T Entidad, double DistanciaMetros)>> GetCercanosAsync(double latitud, double longitud, double radioMetros);

        // Las N entidades mas cercanas al punto dado (sin limite de radio), ordenadas por distancia ascendente
        Task<IReadOnlyList<(T Entidad, double DistanciaMetros)>> GetMasCercanosAsync(double latitud, double longitud, int cantidad);
    }
}
