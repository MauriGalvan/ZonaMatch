using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using DomainContribution = ZonaMatch.Domain.Entities.Contribution;

namespace ZonaMatch.Application.UseCases.Admin;

// Contributions of every zone for the admin validation page: the ones waiting for a decision and the history of the
// latest resolved ones, by an administrator or by the community votes. Approving and rejecting stay in
// ResolveContribution (PUT /api/v1/zones/{slug}/contributions/{id}/resolution).
public sealed class ListAdminContributions(IContributionRepository contributions, IUserRepository users, IZoneProfilesRepository zones)
{
    public const int HistoryLimit = 200;

    public async Task<AdminContributionListResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var pending = await contributions.ListAllPendingAsync(cancellationToken);
        var resolved = await contributions.ListResolvedAsync(HistoryLimit, cancellationToken);
        var zoneNames = await AdminLookups.ZoneNamesAsync(zones, cancellationToken);
        var resolverEmails = await AdminLookups.EmailsAsync(
            users, resolved.Select(contribution => contribution.ResolvedByUserId).OfType<Guid>(), cancellationToken);

        AdminContributionResponse ToResponse(DomainContribution contribution) => new(
            contribution.Id,
            contribution.ZoneSlug,
            zoneNames.GetValueOrDefault(contribution.ZoneSlug) ?? contribution.ZoneSlug,
            contribution.User?.Email ?? string.Empty,
            contribution.Type == ContributionType.NewPoint ? "new-point" : "correction",
            contribution.Name,
            contribution.Category,
            contribution.Address,
            contribution.PointOfInterestName,
            ReasonName(contribution.Reason),
            contribution.Comment,
            contribution.Confirmations,
            contribution.Rejections,
            StatusName(contribution.Status),
            contribution.CreatedAt,
            contribution.ResolvedAt,
            contribution.ResolvedByUserId is { } resolverId ? resolverEmails.GetValueOrDefault(resolverId) : null);

        return new AdminContributionListResponse(pending.Select(ToResponse).ToList(), resolved.Select(ToResponse).ToList());
    }

    private static string StatusName(ContributionStatus status) => status switch
    {
        ContributionStatus.Approved => "approved",
        ContributionStatus.Rejected => "rejected",
        _ => "pending",
    };

    private static string? ReasonName(CorrectionReason? reason) => reason switch
    {
        CorrectionReason.Closed => "closed",
        CorrectionReason.Name => "name",
        CorrectionReason.Location => "location",
        CorrectionReason.Category => "category",
        CorrectionReason.Other => "other",
        _ => null,
    };
}
