using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface IAporteRepository
    {
        Task<(int Aprobados, int Pendientes)> ContarAsync(string zonaSlug, CancellationToken cancellationToken = default);

        // Aportes pendientes de la zona con su autor y sus votos, el mas viejo primero (solo lectura)
        Task<IReadOnlyList<Aporte>> ListarPendientesAsync(string zonaSlug, CancellationToken cancellationToken = default);

        // Con seguimiento de cambios, con sus votos
        Task<Aporte?> ObtenerAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default);

        void Agregar(Aporte aporte);

        Task GuardarCambiosAsync(CancellationToken cancellationToken = default);
    }
}
