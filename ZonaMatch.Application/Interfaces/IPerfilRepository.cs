using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    // Perfil del usuario: su Contexto de tipo perfil (uno por usuario)
    public interface IPerfilRepository
    {
        // Con su detalle y seguimiento de cambios; null si todavia no lo armo
        Task<Contexto?> ObtenerDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);

        // Perfiles existentes de varios usuarios, con sus preferencias y restricciones.
        Task<IReadOnlyList<Contexto>> ObtenerDeUsuariosAsync(IReadOnlyCollection<Guid> usuarioIds, CancellationToken cancellationToken = default);

        void Agregar(Contexto perfil);

        Task GuardarCambiosAsync(CancellationToken cancellationToken = default);
    }
}
