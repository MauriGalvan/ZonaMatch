using Microsoft.Extensions.Logging;
using ZonaMatch.Application.DTOs.Questions.Requests;
using ZonaMatch.Application.DTOs.Questions.Responses;
using ZonaMatch.Application.Emails;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Application.UseCases.Questions
{
    public sealed class AnswerQuestion
    {
        private readonly IQuestionRepository _questions;
        private readonly INotificationRepository _notifications;
        private readonly IEmailService _emailService;
        private readonly ILogger<AnswerQuestion> _logger;
        private readonly TimeProvider _clock;
        private readonly FrontendLinks _links;

        public AnswerQuestion(
            IQuestionRepository questions,
            INotificationRepository notifications,
            IEmailService emailService,
            ILogger<AnswerQuestion> logger,
            TimeProvider clock,
            FrontendLinks links)
        {
            _questions = questions;
            _notifications = notifications;
            _emailService = emailService;
            _logger = logger;
            _clock = clock;
            _links = links;
        }

        public async Task<AnswerResponse> ExecuteAsync(
            string zoneSlug, Guid questionId, AuthenticatedUserContext currentUser, CreateAnswerRequest request, CancellationToken cancellationToken = default)
        {
            var question = await _questions.GetByIdAsync(zoneSlug, questionId, cancellationToken)
                ?? throw new NotFoundException("Question not found.");

            var answer = new Answer
            {
                PreguntaId = questionId,
                UsuarioId = currentUser.Id,
                Texto = request.Text.Trim(),
                FechaCreacion = _clock.GetUtcNow()
            };

            _questions.Add(answer);

            var shouldNotifyAuthor = question.UsuarioId != currentUser.Id;
            if (shouldNotifyAuthor)
            {
                _notifications.AddRange([
                    new UserNotification
                    {
                        UserId = question.UsuarioId,
                        Type = NotificationType.CommunityReply,
                        Title = "Respondieron tu pregunta",
                        Message = $"Alguien respondió una pregunta que publicaste en {zoneSlug}.",
                        Link = $"/zonas/{Uri.EscapeDataString(zoneSlug)}?tab=comunidad",
                        CreatedAt = answer.FechaCreacion
                    }
                ]);
            }

            await _questions.SaveChangesAsync(cancellationToken);

            if (shouldNotifyAuthor && !string.IsNullOrWhiteSpace(question.User?.Email))
            {
                try
                {
                    await _emailService.SendEmailAsync(
                        question.User.Email,
                        "Nueva respuesta a tu pregunta en ZonaMatch",
                        BrandEmail.Render(new BrandEmailContent(
                            Preheader: "Alguien respondió una pregunta que publicaste.",
                            Title: "Respondieron tu pregunta",
                            Paragraphs:
                            [
                                $"Alguien respondió una pregunta que publicaste en la zona <strong>{BrandEmail.Encode(zoneSlug)}</strong>.",
                                "Entrá a Comunidad, en esa zona, para leer la respuesta y seguir la conversación.",
                            ],
                            Button: new BrandEmailButton("Ver la respuesta", _links.To($"/zonas/{Uri.EscapeDataString(zoneSlug)}?tab=comunidad")),
                            Footnote: "Te avisamos porque sos quien hizo la pregunta.")),
                        cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Could not email the author of community question {QuestionId}.", question.Id);
                }
            }

            return QuestionMapper.ToResponse(answer, currentUser.Email, currentUser.Id);
        }
    }
}
