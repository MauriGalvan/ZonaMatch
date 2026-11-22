using ZonaMatch.Application.DTOs.Admin.Requests;
using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.UseCases.Admin;

public sealed class ListAdminSurveys(IAdminSurveyRepository repository)
{
    public const int PageSize = 20;

    public Task<AdminSurveyResponse> ExecuteAsync(AdminSurveyQuery query, CancellationToken cancellationToken = default)
    {
        if (query.From > query.To || query.To == DateOnly.MaxValue)
            throw new BadRequestException("Revisá el rango de fechas: desde debe ser anterior o igual a hasta.");
        var outcome = string.IsNullOrWhiteSpace(query.Outcome) ? null : query.Outcome.Trim();
        if (outcome is not null && !AnalysisFollowUpRules.IsValidOutcome(outcome))
            throw new BadRequestException("El resultado de búsqueda no es válido.");
        if (query.Helpfulness is < 1 or > 5 || query.Page is < 1 or > 1_000_000)
            throw new BadRequestException("La valoración o la página no son válidas.");

        // Dates in the admin panel are whole Argentina days, with an exclusive upper bound.
        static DateTimeOffset StartOfDay(DateOnly day) =>
            new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(-3)).ToUniversalTime();
        return repository.ListAsync(query.From is { } from ? StartOfDay(from) : null,
            query.To is { } to ? StartOfDay(to.AddDays(1)) : null,
            outcome, query.Helpfulness, query.Page, PageSize, cancellationToken);
    }
}
