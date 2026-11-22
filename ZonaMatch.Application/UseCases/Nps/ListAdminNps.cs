using ZonaMatch.Application.DTOs.Nps;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Nps;

public sealed class ListAdminNps(INpsRepository repository)
{
    public Task<AdminNpsResponse> ExecuteAsync(AdminNpsQuery query, CancellationToken cancellationToken = default)
    {
        if (query.From > query.To || query.To == DateOnly.MaxValue || query.Score is < 0 or > 10 || query.Page is < 1 or > 1_000_000)
            throw new BadRequestException("Revisá las fechas, la puntuación y la página seleccionadas.");
        static DateTimeOffset Start(DateOnly day) => new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(-3)).ToUniversalTime();
        return repository.ListResponsesAsync(query.From is { } from ? Start(from) : null,
            query.To is { } to ? Start(to.AddDays(1)) : null, query.Score, query.Page, cancellationToken);
    }
}
