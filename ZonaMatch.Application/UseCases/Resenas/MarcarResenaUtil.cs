using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Resenas
{
    // Marca o desmarca como util una resena de otro usuario
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
                ?? throw new NoEncontradoException("La resena no existe.");

            if (resena.UsuarioId == usuarioId)
                throw new OperacionNoPermitidaException("No podes marcar como util tu propia resena.");

            var voto = resena.VotosUtil.FirstOrDefault(v => v.UsuarioId == usuarioId);

            // Idempotente: marcar dos veces o desmarcar una resena que no estaba marcada no cambia nada
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
