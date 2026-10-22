using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Resenas
{
    // Marks or unmarks another user's review as useful.
    public class MarcarResenaUtil
    {
        private readonly IResenaRepository _resenas;
        private readonly TimeProvider _reloj;

        public MarcarResenaUtil(IResenaRepository resenas, TimeProvider reloj)
        {
            _resenas = resenas;
            _reloj = reloj;
        }

        public async Task EjecutarAsync(
            string zonaSlug, Guid resenaId, Guid usuarioId, bool util, CancellationToken cancellationToken = default)
        {
            var resena = await _resenas.ObtenerAsync(zonaSlug, resenaId, cancellationToken)
                ?? throw new NotFoundException("La resena no existe.");

            if (resena.UsuarioId == usuarioId)
                throw new ConflictException("No podes marcar como util tu propia resena.");

            var voto = resena.VotosUtil.FirstOrDefault(v => v.UsuarioId == usuarioId);

            // Idempotent: repeating either operation does not change the result.
            if (util && voto is null)
                resena.VotosUtil.Add(new ResenaVotoUtil { ResenaId = resena.Id, UsuarioId = usuarioId, Fecha = _reloj.GetUtcNow() });
            else if (!util && voto is not null)
                resena.VotosUtil.Remove(voto);
            else
                return;

            await _resenas.GuardarCambiosAsync(cancellationToken);
        }
    }
}
