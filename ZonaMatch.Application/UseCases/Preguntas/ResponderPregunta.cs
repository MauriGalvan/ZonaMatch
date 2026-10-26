using Microsoft.Extensions.Logging;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Emails;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Application.UseCases.Preguntas
{
    public class ResponderPregunta
    {
        private readonly IPreguntaRepository _preguntas;
        private readonly INotificationRepository _notifications;
        private readonly IEmailService _emailService;
        private readonly ILogger<ResponderPregunta> _logger;
        private readonly TimeProvider _reloj;

        public ResponderPregunta(
            IPreguntaRepository preguntas,
            INotificationRepository notifications,
            IEmailService emailService,
            ILogger<ResponderPregunta> logger,
            TimeProvider reloj)
        {
            _preguntas = preguntas;
            _notifications = notifications;
            _emailService = emailService;
            _logger = logger;
            _reloj = reloj;
        }

        public async Task<RespuestaDto> EjecutarAsync(
            string zonaSlug, Guid preguntaId, AuthenticatedUserContext currentUser, CrearRespuestaDto solicitud, CancellationToken cancellationToken = default)
        {
            var pregunta = await _preguntas.GetByIdAsync(zonaSlug, preguntaId, cancellationToken)
                ?? throw new NotFoundException("Question not found.");

            var respuesta = new Respuesta
            {
                PreguntaId = preguntaId,
                UsuarioId = currentUser.Id,
                Texto = solicitud.Texto.Trim(),
                FechaCreacion = _reloj.GetUtcNow()
            };

            _preguntas.Agregar(respuesta);

            var shouldNotifyAuthor = pregunta.UsuarioId != currentUser.Id;
            if (shouldNotifyAuthor)
            {
                _notifications.AddRange([
                    new UserNotification
                    {
                        UserId = pregunta.UsuarioId,
                        Type = NotificationType.CommunityReply,
                        Title = "Respondieron tu pregunta",
                        Message = $"Alguien respondió una pregunta que publicaste en {zonaSlug}.",
                        Link = $"/zonas/{Uri.EscapeDataString(zonaSlug)}?tab=comunidad",
                        CreatedAt = respuesta.FechaCreacion
                    }
                ]);
            }

            await _preguntas.GuardarCambiosAsync(cancellationToken);

            if (shouldNotifyAuthor && !string.IsNullOrWhiteSpace(pregunta.Usuario?.Email))
            {
                try
                {
                    await _emailService.SendEmailAsync(
                        pregunta.Usuario.Email,
                        "Nueva respuesta a tu pregunta en ZonaMatch",
                        BrandEmail.Render(new BrandEmailContent(
                            Preheader: "Alguien respondió una pregunta que publicaste.",
                            Title: "Respondieron tu pregunta",
                            Paragraphs:
                            [
                                $"Alguien respondió una pregunta que publicaste en la zona <strong>{BrandEmail.Encode(zonaSlug)}</strong>.",
                                "Iniciá sesión en ZonaMatch y entrá a Comunidad, en esa zona, para leer la respuesta.",
                            ],
                            Footnote: "Te avisamos porque sos quien hizo la pregunta.")),
                        cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Could not email the author of community question {QuestionId}.", pregunta.Id);
                }
            }

            return RespuestaMapeo.ADto(respuesta, currentUser.Email, currentUser.Id);
        }
    }
}
