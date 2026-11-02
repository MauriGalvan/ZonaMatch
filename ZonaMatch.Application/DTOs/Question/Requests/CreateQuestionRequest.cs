using System.ComponentModel.DataAnnotations;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.DTOs.Question.Requests;

public sealed record CreateQuestionRequest(
    [Required(ErrorMessage = "El texto de la pregunta es obligatorio.")]
    [StringLength(TextLimits.QuestionText, MinimumLength = TextLimits.QuestionTextMinimum,
        ErrorMessage = "La pregunta debe tener entre 10 y 300 caracteres.")]
    string Text);
