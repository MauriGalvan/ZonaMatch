using System.ComponentModel.DataAnnotations;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.DTOs.Questions.Requests;

public sealed record CreateQuestionRequest(
    [Required(ErrorMessage = "El texto de la pregunta es obligatorio.")]
    [StringLength(Longitudes.TextoPregunta, MinimumLength = Longitudes.TextoPreguntaMinimo,
        ErrorMessage = "La pregunta debe tener entre 10 y 300 caracteres.")]
    string Text);
