using ZonaMatch.Application.DTOs.Criteria.Responses;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Criteria;

// Lists active criteria in display order for the detailed and guest analysis wizards.
public sealed class ListCriteria
{
    private readonly ICriterioRepository _criteria;

    public ListCriteria(ICriterioRepository criteria)
    {
        _criteria = criteria;
    }

    public async Task<IReadOnlyList<CriterionResponse>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var criteria = await _criteria.ListarAsync(cancellationToken);

        return criteria
            .Where(criterion => criterion.Activo)
            .Select(criterion => new CriterionResponse(
                criterion.Id, criterion.Nombre, criterion.Descripcion, criterion.Icono, criterion.Orden))
            .ToList();
    }
}
