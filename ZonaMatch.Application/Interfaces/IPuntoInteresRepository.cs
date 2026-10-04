using ZonaMatch.Application.DTOs;

namespace ZonaMatch.Application.Interfaces
{
    public interface IPuntoInteresRepository
    {
        // Points of the given categories within the radius (meters), closest first, at most "limite" items
        Task<IReadOnlyList<PuntoInteresDto>> GetCercanosAsync(
            double latitud, double longitud, double radioMetros, IReadOnlyCollection<string> categorias, int limite,
            CancellationToken cancellationToken = default);

        // Number of points per (categoria, tipo) within the radius; only pairs with at least one point
        Task<IReadOnlyList<CantidadPorTipo>> ContarPorTipoAsync(
            double latitud, double longitud, double radioMetros, CancellationToken cancellationToken = default);
    }
}
