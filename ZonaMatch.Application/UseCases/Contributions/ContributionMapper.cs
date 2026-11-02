using NetTopologySuite.Geometries;
using ZonaMatch.Application.DTOs.Contributions.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Comunidad;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Contributions
{
    // Maps domain contributions to their API response and normalizes contribution data.
    internal static class ContributionMapper
    {
        public static string ValidateCategory(string? category)
        {
            var code = category?.Trim().ToLowerInvariant();
            if (code is null || !PointOfInterestCategory.All.Contains(code))
                throw new BadRequestException(
                    $"Categoria desconocida. Validas: {string.Join(", ", PointOfInterestCategory.All)}.");

            return code;
        }

        public static Point CreatePoint(double latitude, double longitude) => new(longitude, latitude) { SRID = 4326 };

        public static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        public static ContributionResponse ToResponse(Contribution contribution, string? authorEmail, Guid? currentUserId) =>
            new(
                contribution.Id,
                contribution.Tipo,
                contribution.Estado,
                Autor.Iniciales(authorEmail),
                contribution.FechaCreacion,
                contribution.UsuarioId == currentUserId,
                contribution.Validaciones.FirstOrDefault(v => v.UsuarioId == currentUserId)?.Confirma,
                contribution.Confirmaciones,
                contribution.Rechazos,
                contribution.Nombre,
                contribution.Categoria,
                contribution.Ubicacion is null ? null : new ContributionLocationResponse(contribution.Ubicacion.Y, contribution.Ubicacion.X),
                contribution.Horario,
                contribution.Direccion,
                contribution.ViveOTrabajaEnZona,
                contribution.PuntoInteresId,
                contribution.PuntoInteresNombre,
                contribution.Motivo,
                contribution.Comentario);
    }
}
