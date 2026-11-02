using ZonaMatch.Application.DTOs.Questions.Requests;
using ZonaMatch.Application.DTOs.Questions.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Comunidad;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Application.UseCases.Questions
{
    public sealed class CreateQuestion
    {
        private readonly IPreguntaRepository _questions;
        private readonly IZonaRepository _zones;
        private readonly ISavedZoneRepository _savedZones;
        private readonly INotificationRepository _notifications;
        private readonly TimeProvider _clock;

        public CreateQuestion(
            IPreguntaRepository questions,
            IZonaRepository zones,
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
            await _zones.ValidarExisteAsync(zoneSlug, cancellationToken);

            var question = new Pregunta
            {
                ZonaSlug = zoneSlug,
                UsuarioId = currentUser.Id,
                Texto = request.Text.Trim(),
                FechaCreacion = _clock.GetUtcNow()
            };

            _questions.Agregar(question);

            var savedZones = await _savedZones.GetForZoneAsync(zoneSlug, currentUser.Id, cancellationToken);
            _notifications.AddRange(savedZones.Select(savedZone => new UserNotification
            {
                UserId = savedZone.UserId,
                Type = NotificationType.SavedZoneQuestion,
                Title = "Nueva pregunta en una zona guardada",
                Message = $"Publicaron una nueva pregunta en {savedZone.Name}.",
                Link = $"/zonas/{Uri.EscapeDataString(zoneSlug)}?tab=comunidad",
                CreatedAt = question.FechaCreacion
            }));

            await _questions.GuardarCambiosAsync(cancellationToken);

            return new QuestionResponse(question.Id, question.Texto, Autor.Iniciales(currentUser.Email), question.FechaCreacion, IsMine: true, []);
        }
    }
}
