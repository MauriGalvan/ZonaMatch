using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Comunidad;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Preguntas
{
    public class CrearPregunta
    {
        private readonly IPreguntaRepository _preguntas;
        private readonly IZonaRepository _zonas;
        private readonly TimeProvider _reloj;

        public CrearPregunta(IPreguntaRepository preguntas, IZonaRepository zonas, TimeProvider reloj)
        {
            _preguntas = preguntas;
            _zonas = zonas;
            _reloj = reloj;
        }

        public async Task<PreguntaDto> EjecutarAsync(
            string zonaSlug, SesionDto usuario, CrearPreguntaDto solicitud, CancellationToken cancellationToken = default)
        {
            await _zonas.ValidarExisteAsync(zonaSlug, cancellationToken);

            var pregunta = new Pregunta
            {
                ZonaSlug = zonaSlug,
                UsuarioId = usuario.Id,
                Texto = solicitud.Texto.Trim(),
                FechaCreacion = _reloj.GetUtcNow()
            };

            _preguntas.Agregar(pregunta);
            await _preguntas.GuardarCambiosAsync(cancellationToken);

            return new PreguntaDto(pregunta.Id, pregunta.Texto, Autor.Iniciales(usuario.Email), pregunta.FechaCreacion, EsMia: true, []);
        }
    }
}
