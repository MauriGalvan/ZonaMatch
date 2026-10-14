using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    // Analisis guardados. Se leen siempre con su criterio y sus zonas, y con seguimiento de cambios:
    // lo modificado se guarda con GuardarCambiosAsync.
    public interface IAnalisisRepository
    {
        // Originales y versiones del usuario, el mas reciente primero
        Task<IReadOnlyList<Analisis>> ListarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);

        // null si no existe o es de otro usuario
        Task<Analisis?> ObtenerAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default);

        // Versiones derivadas de un original, la mas vieja primero
        Task<IReadOnlyList<Analisis>> ListarVersionesAsync(Guid analisisOrigenId, CancellationToken cancellationToken = default);

        void Agregar(Analisis analisis);

        // Borra el analisis junto con la copia de su criterio
        void Eliminar(Analisis analisis);

        Task GuardarCambiosAsync(CancellationToken cancellationToken = default);
    }
}
