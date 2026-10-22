using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Comunidad
{
    // Validations shared by community use cases that write to a zone.
    internal static class ZonaRepositoryExtensions
    {
        public static async Task ValidarExisteAsync(this IZonaRepository zonas, string zonaSlug, CancellationToken cancellationToken)
        {
            if (!await zonas.ExisteAsync(zonaSlug, cancellationToken))
                throw new NotFoundException("No se encontro una zona con ese identificador.");
        }

        // A missing zone is a NotFoundException; a point outside a known zone is a BadRequestException.
        public static async Task ValidarContienePuntoAsync(
            this IZonaRepository zonas, string zonaSlug, double latitud, double longitud, CancellationToken cancellationToken)
        {
            if (await zonas.ContienePuntoAsync(zonaSlug, latitud, longitud, cancellationToken))
                return;

            await zonas.ValidarExisteAsync(zonaSlug, cancellationToken);
            throw new BadRequestException("El punto tiene que estar dentro de la zona.");
        }
    }
}
