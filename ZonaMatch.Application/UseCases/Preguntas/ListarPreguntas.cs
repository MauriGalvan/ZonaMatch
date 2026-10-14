using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Comunidad;

namespace ZonaMatch.Application.UseCases.Preguntas
{
    // Preguntas de una zona, la mas nueva primero, cada una con sus respuestas (la mas vieja primero)
    public class ListarPreguntas
    {
        private readonly IPreguntaRepository _preguntas;

        public ListarPreguntas(IPreguntaRepository preguntas)
        {
            _preguntas = preguntas;
        }

        public async Task<IReadOnlyList<PreguntaDto>> EjecutarAsync(
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
                        .Select(r => RespuestaMapeo.ADto(r, r.Usuario?.Email, usuarioActual))
                        .ToList()))
                .ToList();
        }
    }
}
