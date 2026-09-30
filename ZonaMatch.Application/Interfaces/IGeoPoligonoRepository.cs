using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.Interfaces
{
    // Repositorio para entidades representadas por un area (poligono/multipoligono)
    public interface IGeoPoligonoRepository<T> : IRepository<T> where T : class, IUbicacionPoligono
    {
        // La entidad cuya area contiene el punto dado (ej: el barrio al que pertenece una direccion)
        Task<T?> GetQueContienePuntoAsync(double latitud, double longitud);

        // Entidades cuya area se encuentra (aproximadamente) dentro del radio dado, en metros, desde el punto
        Task<IReadOnlyList<T>> GetCercanosAsync(double latitud, double longitud, double radioMetros);
    }
}
