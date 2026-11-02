using System.ComponentModel.DataAnnotations;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.DTOs.Questions.Requests;

public sealed record CreateAnswerRequest(
    [Required(ErrorMessage = "El texto de la respuesta es obligatorio.")]
    [StringLength(Longitudes.TextoRespuesta, MinimumLength = Longitudes.TextoRespuestaMinimo,
        ErrorMessage = "La respuesta debe tener entre 2 y 1000 caracteres.")]
    string Text);
