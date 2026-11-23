using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Admin;

// The history of questions administrators deleted, for the Questions tab of the admin validation page.
public sealed class ListDeletedQuestions(IQuestionRepository questions, IZoneProfilesRepository zones)
{
    public const int Limit = 200;

    public async Task<IReadOnlyList<AdminDeletedQuestionResponse>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var deleted = await questions.ListDeletedAsync(Limit, cancellationToken);
        var zoneNames = await AdminLookups.ZoneNamesAsync(zones, cancellationToken);

        return deleted
            .Select(question => new AdminDeletedQuestionResponse(
                question.Id,
                question.ZoneSlug,
                zoneNames.GetValueOrDefault(question.ZoneSlug) ?? question.ZoneSlug,
                question.Text,
                question.AuthorEmail,
                question.AskedAt,
                question.AnswerCount,
                question.DeletedAt,
                question.DeletedByEmail))
            .ToList();
    }
}
