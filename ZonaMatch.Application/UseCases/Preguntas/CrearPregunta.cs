using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Comunidad;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Application.UseCases.Preguntas
{
    public class CrearPregunta
    {
        private readonly IPreguntaRepository _preguntas;
        private readonly IZonaRepository _zonas;
        private readonly ISavedZoneRepository _savedZones;
        private readonly INotificationRepository _notifications;
        private readonly TimeProvider _reloj;

        public CrearPregunta(
            IPreguntaRepository preguntas,
            IZonaRepository zonas,
            ISavedZoneRepository savedZones,
            INotificationRepository notifications,
            TimeProvider reloj)
        {
            _preguntas = preguntas;
            _zonas = zonas;
            _savedZones = savedZones;
            _notifications = notifications;
            _reloj = reloj;
        }

        public async Task<PreguntaDto> EjecutarAsync(
            string zonaSlug, AuthenticatedUserContext currentUser, CrearPreguntaDto solicitud, CancellationToken cancellationToken = default)
        {
            await _zonas.ValidarExisteAsync(zonaSlug, cancellationToken);

            var pregunta = new Pregunta
            {
                ZonaSlug = zonaSlug,
                UsuarioId = currentUser.Id,
                Texto = solicitud.Texto.Trim(),
                FechaCreacion = _reloj.GetUtcNow()
            };

            _preguntas.Agregar(pregunta);

            var savedZones = await _savedZones.GetForZoneAsync(zonaSlug, currentUser.Id, cancellationToken);
            _notifications.AddRange(savedZones.Select(savedZone => new UserNotification
            {
                UserId = savedZone.UserId,
                Type = NotificationType.SavedZoneQuestion,
                Title = "Nueva pregunta en una zona guardada",
                Message = $"Publicaron una nueva pregunta en {savedZone.Name}.",
                Link = $"/zonas/{Uri.EscapeDataString(zonaSlug)}?tab=comunidad",
                CreatedAt = pregunta.FechaCreacion
            }));

            await _preguntas.GuardarCambiosAsync(cancellationToken);

            return new PreguntaDto(pregunta.Id, pregunta.Texto, Autor.Iniciales(currentUser.Email), pregunta.FechaCreacion, EsMia: true, []);
        }
    }
}
