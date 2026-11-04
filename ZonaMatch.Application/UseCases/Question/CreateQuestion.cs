using ZonaMatch.Application.DTOs.Question.Requests;
using ZonaMatch.Application.DTOs.Question.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Community;
using ZonaMatch.Domain.Entities;
using DomainQuestion = ZonaMatch.Domain.Entities.Question;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Application.UseCases.Question
{
    public sealed class CreateQuestion
    {
        private readonly IQuestionRepository _questions;
        private readonly IZoneRepository _zones;
        private readonly ISavedZoneRepository _savedZones;
        private readonly INotificationRepository _notifications;
        private readonly TimeProvider _clock;

        public CreateQuestion(
            IQuestionRepository questions,
            IZoneRepository zones,
            ISavedZoneRepository savedZones,
            INotificationRepository notifications,
            TimeProvider clock)
        {
            _questions = questions;
            _zones = zones;
            _savedZones = savedZones;
            _notifications = notifications;
            _clock = clock;
        }

        public async Task<QuestionResponse> ExecuteAsync(
            string zoneSlug, AuthenticatedUserContext currentUser, CreateQuestionRequest request, CancellationToken cancellationToken = default)
        {
            await _zones.EnsureZoneExistsAsync(zoneSlug, cancellationToken);

            var question = new DomainQuestion
            {
                ZoneSlug = zoneSlug,
                UserId = currentUser.Id,
                Text = request.Text.Trim(),
                CreatedAt = _clock.GetUtcNow()
            };

            _questions.Add(question);

            var savedZones = await _savedZones.GetForZoneAsync(zoneSlug, currentUser.Id, cancellationToken);
            _notifications.AddRange(savedZones.Select(savedZone => new UserNotification
            {
                UserId = savedZone.UserId,
                Type = NotificationType.SavedZoneQuestion,
                Title = "Nueva pregunta en una zona guardada",
                Message = $"Publicaron una nueva pregunta en {savedZone.Name}.",
                Link = $"/zonas/{Uri.EscapeDataString(zoneSlug)}?tab=comunidad",
                CreatedAt = question.CreatedAt
            }));

            await _questions.SaveChangesAsync(cancellationToken);

            return new QuestionResponse(question.Id, question.Text, AuthorInitials.GetInitials(currentUser.Email), question.CreatedAt, IsMine: true, []);
        }
    }
}
