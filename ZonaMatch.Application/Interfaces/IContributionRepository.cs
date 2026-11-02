using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface IContributionRepository
    {
        Task<(int Aprobados, int Pendientes)> ContarAsync(string zonaSlug, CancellationToken cancellationToken = default);

        // Aportes pendientes de la zona con su autor y sus votos, el mas viejo primero (solo lectura)
        Task<IReadOnlyList<Contribution>> ListarPendientesAsync(string zonaSlug, CancellationToken cancellationToken = default);

        // Con seguimiento de cambios, con sus votos
        Task<Contribution?> ObtenerAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default);

        void Agregar(Contribution aporte);

        Task GuardarCambiosAsync(CancellationToken cancellationToken = default);
    }
}
