using ZonaMatch.Application.DTOs.AnalysisFollowUp.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.UseCases.AnalysisFollowUp;

public sealed class SubmitAnalysisFollowUp(IAnalysisFollowUpRepository repository, TimeProvider clock)
{
    public async Task ExecuteAsync(Guid id, Guid userId, SubmitAnalysisFollowUpRequest request, CancellationToken cancellationToken = default)
    {
        var followUp = await repository.GetAsync(id, userId, cancellationToken)
            ?? throw new NotFoundException("El seguimiento no existe.");
        if (followUp.CompletedAt is not null)
            throw new ConflictException("Ya respondiste el seguimiento de este análisis.");
        if (!AnalysisFollowUpRules.IsValidOutcome(request.Outcome))
            throw new BadRequestException("Elegí cómo siguió tu búsqueda.");
        if (request.Helpfulness is not (>= 1 and <= 5))
            throw new BadRequestException("Indicá cuánto te ayudó ZonaMatch, del 1 al 5.");
        if (request.Comment?.Length > AnalysisFollowUpRules.MaximumCommentLength)
            throw new BadRequestException("El comentario puede tener hasta 2.000 caracteres.");

        var preferences = await repository.GetPreferencesAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Las preferencias de encuestas no existen.");
        var now = clock.GetUtcNow();
        followUp.Outcome = request.Outcome;
        followUp.Helpfulness = request.Helpfulness;
        followUp.Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        followUp.CompletedAt = now;
        followUp.PostponedUntil = null;
        preferences.NextInvitationAt = now.AddDays(AnalysisFollowUpRules.InvitationIntervalDays);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
