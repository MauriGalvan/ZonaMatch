using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Preguntas
{
    public class ResponderPregunta
    {
        private readonly IPreguntaRepository _preguntas;
        private readonly TimeProvider _reloj;

        public ResponderPregunta(IPreguntaRepository preguntas, TimeProvider reloj)
        {
            _preguntas = preguntas;
            _reloj = reloj;
        }

        public async Task<RespuestaDto> EjecutarAsync(
            string zonaSlug, Guid preguntaId, AuthenticatedUserContext currentUser, CrearRespuestaDto solicitud, CancellationToken cancellationToken = default)
        {
            if (!await _preguntas.ExisteAsync(zonaSlug, preguntaId, cancellationToken))
                throw new NotFoundException("La pregunta no existe.");

            var respuesta = new Respuesta
            {
                PreguntaId = preguntaId,
                UsuarioId = currentUser.Id,
                Texto = solicitud.Texto.Trim(),
                FechaCreacion = _reloj.GetUtcNow()
            };

            _preguntas.Agregar(respuesta);
            await _preguntas.GuardarCambiosAsync(cancellationToken);

            return RespuestaMapeo.ADto(respuesta, currentUser.Email, currentUser.Id);
        }
    }
}
