using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Community
{
    // Validations shared by community use cases that write to a zone.
    internal static class ZoneRepositoryExtensions
    {
        public static async Task EnsureZoneExistsAsync(this IZoneRepository zones, string zoneSlug, CancellationToken cancellationToken)
        {
            if (!await zones.ExistsAsync(zoneSlug, cancellationToken))
                throw new NotFoundException("No se encontró una zona con ese identificador.");
        }

        // A missing zone is a NotFoundException; a point outside a known zone is a BadRequestException.
        public static async Task EnsurePointBelongsToZoneAsync(
            this IZoneRepository zones, string zoneSlug, double latitude, double longitude, CancellationToken cancellationToken)
        {
            if (await zones.ContainsPointAsync(zoneSlug, latitude, longitude, cancellationToken))
                return;

            await zones.EnsureZoneExistsAsync(zoneSlug, cancellationToken);
            throw new BadRequestException("El punto tiene que estar dentro de la zona.");
        }
    }
}
