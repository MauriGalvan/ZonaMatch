using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    // Analysis guardados. Se leen siempre con su criterio y sus zonas, y con seguimiento de cambios:
    // lo modificado se guarda con GuardarCambiosAsync.
    public interface IAnalisisRepository
    {
        // Originales y versiones del usuario, el mas reciente primero
        Task<IReadOnlyList<Analysis>> ListarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);

        // null si no existe o es de otro usuario
        Task<Analysis?> ObtenerAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default);

        // Versiones derivadas de un original, la mas vieja primero
        Task<IReadOnlyList<Analysis>> ListarVersionesAsync(Guid analisisOrigenId, CancellationToken cancellationToken = default);

        void Agregar(Analysis analisis);

        // Borra el analisis junto con la copia de su criterio
        void Eliminar(Analysis analisis);

        Task GuardarCambiosAsync(CancellationToken cancellationToken = default);
    }
}
