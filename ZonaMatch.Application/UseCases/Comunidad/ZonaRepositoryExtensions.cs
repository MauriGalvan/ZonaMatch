using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Comunidad
{
    // Validaciones que comparten los casos de uso de la comunidad que escriben sobre una zona
    internal static class ZonaRepositoryExtensions
    {
        public static async Task ValidarExisteAsync(this IZonaRepository zonas, string zonaSlug, CancellationToken cancellationToken)
        {
            if (!await zonas.ExisteAsync(zonaSlug, cancellationToken))
                throw new NoEncontradoException("No se encontro una zona con ese identificador.");
        }

        // Una zona desconocida es NoEncontradoException; un punto fuera de una zona conocida, DatosInvalidosException
        public static async Task ValidarContienePuntoAsync(
            this IZonaRepository zonas, string zonaSlug, double latitud, double longitud, CancellationToken cancellationToken)
        {
            if (await zonas.ContienePuntoAsync(zonaSlug, latitud, longitud, cancellationToken))
                return;

            await zonas.ValidarExisteAsync(zonaSlug, cancellationToken);
            throw new DatosInvalidosException("El punto tiene que estar dentro de la zona.");
        }
    }
}
