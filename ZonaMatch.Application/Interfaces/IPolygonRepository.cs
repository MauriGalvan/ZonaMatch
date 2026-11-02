using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.Interfaces
{
    // Repositorio para entidades representadas por un area (poligono/multipoligono)
    public interface IPolygonRepository<T> : IRepository<T> where T : class, IPolygonLocation
    {
        // La entidad cuya area contiene el punto dado (ej: el barrio al que pertenece una direccion)
        Task<T?> GetContainingPointAsync(double latitud, double longitud);

        // Entidades cuya area se encuentra (aproximadamente) dentro del radio dado, en metros, desde el punto
        Task<IReadOnlyList<T>> GetNearbyAsync(double latitud, double longitud, double radioMetros);
    }
}
