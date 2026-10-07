using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Services
{
    public class PreguntaService : IPreguntaService
    {
        private readonly IPreguntaRepository _preguntas;
        private readonly IZonaRepository _zonas;
        private readonly TimeProvider _timeProvider;

        public PreguntaService(IPreguntaRepository preguntas, IZonaRepository zonas, TimeProvider timeProvider)
        {
            _preguntas = preguntas;
            _zonas = zonas;
            _timeProvider = timeProvider;
        }

        public async Task<IReadOnlyList<PreguntaDto>> ListarAsync(
            string zonaSlug, Guid? usuarioActual, CancellationToken cancellationToken = default)
        {
            var preguntas = await _preguntas.ListarPorZonaAsync(zonaSlug, cancellationToken);

            return preguntas
                .Select(p => new PreguntaDto(
                    p.Id,
                    p.Texto,
                    Autor.Iniciales(p.Usuario?.Email),
                    p.FechaCreacion,
                    p.UsuarioId == usuarioActual,
                    p.Respuestas
                        .OrderBy(r => r.FechaCreacion)
                        .Select(r => ADto(r, r.Usuario?.Email, usuarioActual))
                        .ToList()))
                .ToList();
        }

        public async Task<PreguntaDto> CrearAsync(
            string zonaSlug, SesionDto usuario, CrearPreguntaDto request, CancellationToken cancellationToken = default)
        {
            if (!await _zonas.ExisteAsync(zonaSlug, cancellationToken))
                throw new NoEncontradoException("No se encontro una zona con ese identificador.");

            var pregunta = new Pregunta
            {
                ZonaSlug = zonaSlug,
                UsuarioId = usuario.Id,
                Texto = request.Texto.Trim(),
                FechaCreacion = _timeProvider.GetUtcNow()
            };

            _preguntas.Agregar(pregunta);
            await _preguntas.GuardarCambiosAsync(cancellationToken);

            return new PreguntaDto(pregunta.Id, pregunta.Texto, Autor.Iniciales(usuario.Email), pregunta.FechaCreacion, EsMia: true, []);
        }

        public async Task<RespuestaDto> ResponderAsync(
            string zonaSlug, Guid preguntaId, SesionDto usuario, CrearRespuestaDto request, CancellationToken cancellationToken = default)
        {
            if (!await _preguntas.ExisteAsync(zonaSlug, preguntaId, cancellationToken))
                throw new NoEncontradoException("La pregunta no existe.");

            var respuesta = new Respuesta
            {
                PreguntaId = preguntaId,
                UsuarioId = usuario.Id,
                Texto = request.Texto.Trim(),
                FechaCreacion = _timeProvider.GetUtcNow()
            };

            _preguntas.Agregar(respuesta);
            await _preguntas.GuardarCambiosAsync(cancellationToken);

            return ADto(respuesta, usuario.Email, usuario.Id);
        }

        private static RespuestaDto ADto(Respuesta respuesta, string? emailAutor, Guid? usuarioActual) =>
            new(respuesta.Id, respuesta.Texto, Autor.Iniciales(emailAutor), respuesta.FechaCreacion, respuesta.UsuarioId == usuarioActual);
    }
}
