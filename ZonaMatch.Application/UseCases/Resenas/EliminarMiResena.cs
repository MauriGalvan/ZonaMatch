using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Resenas
{
    public class EliminarMiResena
    {
        private readonly IResenaRepository _resenas;

        public EliminarMiResena(IResenaRepository resenas)
        {
            _resenas = resenas;
        }

        public async Task EjecutarAsync(string zonaSlug, Guid usuarioId, CancellationToken cancellationToken = default)
        {
            var resena = await _resenas.ObtenerDeUsuarioAsync(zonaSlug, usuarioId, cancellationToken)
                ?? throw new NoEncontradoException("No escribiste una resena de esta zona.");

            _resenas.Eliminar(resena);
            await _resenas.GuardarCambiosAsync(cancellationToken);
        }
    }
}
