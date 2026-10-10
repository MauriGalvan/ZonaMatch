using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface IPreguntaRepository
    {
        // Preguntas de la zona con sus respuestas y autores, la pregunta mas nueva primero (solo lectura)
        Task<IReadOnlyList<Pregunta>> ListarPorZonaAsync(string zonaSlug, CancellationToken cancellationToken = default);

        Task<bool> ExisteAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default);

        void Agregar(Pregunta pregunta);
        void Agregar(Respuesta respuesta);

        Task GuardarCambiosAsync(CancellationToken cancellationToken = default);
    }
}
