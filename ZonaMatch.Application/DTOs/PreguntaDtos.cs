using System.ComponentModel.DataAnnotations;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.DTOs
{
    // Pregunta con sus respuestas, la respuesta mas vieja primero. EsMia es false en pedidos anonimos.
    public record PreguntaDto(
        Guid Id,
        string Texto,
        string Iniciales,
        DateTimeOffset Fecha,
        bool EsMia,
        IReadOnlyList<RespuestaDto> Respuestas);

    public record RespuestaDto(Guid Id, string Texto, string Iniciales, DateTimeOffset Fecha, bool EsMia);

    // POST /Zonas/{slug}/preguntas
    public record CrearPreguntaDto(
        [Required(ErrorMessage = "El texto de la pregunta es obligatorio.")]
        [StringLength(Longitudes.TextoPregunta, MinimumLength = Longitudes.TextoPreguntaMinimo,
            ErrorMessage = "La pregunta debe tener entre 10 y 300 caracteres.")]
        string Texto);

    // POST /Zonas/{slug}/preguntas/{id}/respuestas
    public record CrearRespuestaDto(
        [Required(ErrorMessage = "El texto de la respuesta es obligatorio.")]
        [StringLength(Longitudes.TextoRespuesta, MinimumLength = Longitudes.TextoRespuestaMinimo,
            ErrorMessage = "La respuesta debe tener entre 2 y 1000 caracteres.")]
        string Texto);
}
