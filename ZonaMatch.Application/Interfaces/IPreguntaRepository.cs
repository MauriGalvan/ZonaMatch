using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface IPreguntaRepository
    {
        // Preguntas de la zona con sus respuestas y autores, la pregunta mas nueva primero (solo lectura)
        Task<IReadOnlyList<Question>> ListarPorZonaAsync(string zonaSlug, CancellationToken cancellationToken = default);

        Task<bool> ExisteAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default);

        Task<Question?> GetByIdAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default);

        void Agregar(Question pregunta);
        void Agregar(Answer respuesta);

        Task GuardarCambiosAsync(CancellationToken cancellationToken = default);
    }
}
