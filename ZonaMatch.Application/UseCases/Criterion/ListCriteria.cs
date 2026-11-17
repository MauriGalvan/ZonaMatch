using ZonaMatch.Application.DTOs.Criterion.Responses;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Criterion;

// Lists active criteria in display order for the wizards: the household profile and guest analysis by default, or
// the investor profile.
public sealed class ListCriteria
{
    private readonly ICriterionRepository _criteria;

    public ListCriteria(ICriterionRepository criteria)
    {
        _criteria = criteria;
    }

    public const string InvestorProfile = "inversor";

    public async Task<IReadOnlyList<CriterionResponse>> ExecuteAsync(string? profile = null, CancellationToken cancellationToken = default)
    {
        var criteria = await _criteria.ListAsync(cancellationToken);
        var forInvestor = profile == InvestorProfile;

        return criteria
            .Where(criterion => criterion.IsActive && (forInvestor ? criterion.ForInvestor : criterion.ForHousehold))
            .Select(criterion => new CriterionResponse(
                criterion.Id, criterion.Name, criterion.Description, criterion.IconCssClass, criterion.Order))
            .ToList();
    }
}
