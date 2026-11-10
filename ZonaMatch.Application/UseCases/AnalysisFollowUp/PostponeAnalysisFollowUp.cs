using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.UseCases.AnalysisFollowUp;

public sealed class PostponeAnalysisFollowUp(IAnalysisFollowUpRepository repository, TimeProvider clock)
{
    public async Task ExecuteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var followUp = await repository.GetAsync(id, userId, cancellationToken)
            ?? throw new NotFoundException("El seguimiento no existe.");
        if (followUp.CompletedAt is not null)
            throw new ConflictException("Ya respondiste el seguimiento de este análisis.");
        var preferences = await repository.GetPreferencesAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Las preferencias de encuestas no existen.");
        var nextInvitation = clock.GetUtcNow().AddDays(AnalysisFollowUpRules.PostponeDays);
        followUp.PostponedUntil = nextInvitation;
        preferences.NextInvitationAt = nextInvitation;
        await repository.SaveChangesAsync(cancellationToken);
    }
}
