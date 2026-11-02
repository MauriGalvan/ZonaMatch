using ZonaMatch.Application.DTOs.Criterion.Responses;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Criteria;

// Lists active criteria in display order for the detailed and guest analysis wizards.
public sealed class ListCriteria
{
    private readonly ICriterionRepository _criteria;

    public ListCriteria(ICriterionRepository criteria)
    {
        _criteria = criteria;
    }

    public async Task<IReadOnlyList<CriterionResponse>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var criteria = await _criteria.ListAsync(cancellationToken);

        return criteria
            .Where(criterion => criterion.IsActive)
            .Select(criterion => new CriterionResponse(
                criterion.Id, criterion.Name, criterion.Description, criterion.IconCssClass, criterion.Order))
            .ToList();
    }
}
