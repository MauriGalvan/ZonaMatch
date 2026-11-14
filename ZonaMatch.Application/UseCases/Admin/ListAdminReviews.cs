using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using DomainReview = ZonaMatch.Domain.Entities.Review;

namespace ZonaMatch.Application.UseCases.Admin;

// Reviews for the admin moderation page: the ones waiting for approval and the history of the latest resolved ones,
// with the zone name and who resolved each one. An approved review that its author edits goes back to pending, so it
// leaves the history until it is resolved again.
public sealed class ListAdminReviews(IReviewRepository reviews, IUserRepository users, IZoneProfilesRepository zones)
{
    public const int HistoryLimit = 200;

    public async Task<AdminReviewListResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var pending = await reviews.ListPendingAsync(cancellationToken);
        var resolved = await reviews.ListResolvedAsync(HistoryLimit, cancellationToken);

        var zoneNames = (await zones.ListAsync(cancellationToken))
            .GroupBy(zone => zone.Slug)
            .ToDictionary(group => group.Key, group => group.First().Name);

        // Only a few administrators resolve reviews, so they are read one by one.
        var resolverEmails = new Dictionary<Guid, string?>();
        foreach (var resolverId in resolved.Select(review => review.ResolvedByUserId).OfType<Guid>().Distinct())
            resolverEmails[resolverId] = (await users.GetByIdAsync(resolverId, cancellationToken))?.Email;

        AdminReviewResponse ToResponse(DomainReview review) => new(
            review.Id,
            review.ZoneSlug,
            zoneNames.GetValueOrDefault(review.ZoneSlug) ?? review.ZoneSlug,
            review.User?.Email ?? string.Empty,
            review.OverallScore,
            review.Text,
            review.CreatedAt,
            review.UpdatedAt,
            StatusName(review.Status),
            review.RejectionReason,
            review.ResolvedAt,
            review.ResolvedByUserId is { } resolverId ? resolverEmails.GetValueOrDefault(resolverId) : null);

        return new AdminReviewListResponse(pending.Select(ToResponse).ToList(), resolved.Select(ToResponse).ToList());
    }

    private static string StatusName(ReviewStatus status) => status switch
    {
        ReviewStatus.Approved => "approved",
        ReviewStatus.Rejected => "rejected",
        _ => "pending",
    };
}
