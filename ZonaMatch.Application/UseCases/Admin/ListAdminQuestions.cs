using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Admin;

// The latest questions of every zone for the admin validation page, newest first, with their answers. Questions are
// published right away; the administrator follows them and can delete an inappropriate one (DeleteAdminQuestion).
public sealed class ListAdminQuestions(IQuestionRepository questions, IZoneProfilesRepository zones)
{
    public const int Limit = 200;

    public async Task<IReadOnlyList<AdminQuestionResponse>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var latest = await questions.ListLatestAsync(Limit, cancellationToken);
        var zoneNames = await AdminLookups.ZoneNamesAsync(zones, cancellationToken);

        return latest
            .Select(question => new AdminQuestionResponse(
                question.Id,
                question.ZoneSlug,
                zoneNames.GetValueOrDefault(question.ZoneSlug) ?? question.ZoneSlug,
                question.User?.Email ?? string.Empty,
                question.Text,
                question.CreatedAt,
                question.Answers
                    .OrderBy(answer => answer.CreatedAt)
                    .Select(answer => new AdminAnswerResponse(answer.Id, answer.User?.Email ?? string.Empty, answer.Text, answer.CreatedAt))
                    .ToList()))
            .ToList();
    }
}
