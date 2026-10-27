using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.Analysis.Requests;

public sealed class SetAnalysisFavoriteRequest
{
    [Required(ErrorMessage = "Indicá si el análisis es favorito.")]
    public bool? IsFavorite { get; set; }
}
