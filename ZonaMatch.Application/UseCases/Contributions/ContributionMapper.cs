using NetTopologySuite.Geometries;
using ZonaMatch.Application.DTOs.Contribution.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Community;
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
                contribution.Type,
                contribution.Status,
                AuthorInitials.GetInitials(authorEmail),
                contribution.CreatedAt,
                contribution.UserId == currentUserId,
                contribution.Votes.FirstOrDefault(vote => vote.UserId == currentUserId)?.Confirms,
                contribution.Confirmations,
                contribution.Rejections,
                contribution.Name,
                contribution.Category,
                contribution.Location is null ? null : new ContributionLocationResponse(contribution.Location.Y, contribution.Location.X),
                contribution.OpeningHours,
                contribution.Address,
                contribution.LivesOrWorksInZone,
                contribution.PointOfInterestId,
                contribution.PointOfInterestName,
                contribution.Reason,
                contribution.Comment);
    }
}
