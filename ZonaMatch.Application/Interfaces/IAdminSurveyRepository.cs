using ZonaMatch.Application.DTOs.Admin.Responses;

namespace ZonaMatch.Application.Interfaces;

public interface IAdminSurveyRepository
{
    Task<AdminSurveyResponse> ListAsync(DateTimeOffset? from, DateTimeOffset? until,
        string? outcome, int? helpfulness, int page, int pageSize, CancellationToken cancellationToken = default);
}
