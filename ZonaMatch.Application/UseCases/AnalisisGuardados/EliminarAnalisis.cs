using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.AnalisisGuardados
{
    // Borra un analisis con la copia de su criterio. Borrar un original borra tambien sus versiones.
    public class EliminarAnalisis
    {
        private readonly IAnalisisRepository _analisis;

        public EliminarAnalisis(IAnalisisRepository analisis)
        {
            _analisis = analisis;
        }

        public async Task EjecutarAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default)
        {
            var analisis = await _analisis.ObtenerAsync(id, usuarioId, cancellationToken)
                ?? throw new AnalisisNoEncontradoException();

            if (analisis.AnalisisOrigenId is null)
            {
                foreach (var version in await _analisis.ListarVersionesAsync(analisis.Id, cancellationToken))
                    _analisis.Eliminar(version);
            }

            _analisis.Eliminar(analisis);
            await _analisis.GuardarCambiosAsync(cancellationToken);
        }
    }
}
