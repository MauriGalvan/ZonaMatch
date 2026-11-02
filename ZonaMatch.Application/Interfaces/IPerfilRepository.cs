using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    // Perfil del usuario: su Context de tipo perfil (uno por usuario)
    public interface IPerfilRepository
    {
        // Con su detalle y seguimiento de cambios; null si todavia no lo armo
        Task<Context?> ObtenerDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);

        // Perfiles existentes de varios usuarios, con sus preferencias y restricciones.
        Task<IReadOnlyList<Context>> ObtenerDeUsuariosAsync(IReadOnlyCollection<Guid> usuarioIds, CancellationToken cancellationToken = default);

        void Agregar(Context perfil);

        Task GuardarCambiosAsync(CancellationToken cancellationToken = default);
    }
}
