using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface IPreguntaRepository
    {
        // Questions of the zone with their answers and authors, newest question first (read only)
        Task<IReadOnlyList<Pregunta>> ListarPorZonaAsync(string zonaSlug, CancellationToken cancellationToken = default);

        Task<bool> ExisteAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default);

        void Agregar(Pregunta pregunta);
        void Agregar(Respuesta respuesta);

        Task GuardarCambiosAsync(CancellationToken cancellationToken = default);
    }
}
