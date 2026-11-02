using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.Reviews.Requests;

public sealed record ReviewAspectScoresRequest(
    [Range(1, 5, ErrorMessage = "El puntaje de seguridad debe estar entre 1 y 5.")] int Safety,
    [Range(1, 5, ErrorMessage = "El puntaje de transporte debe estar entre 1 y 5.")] int Transport,
    [Range(1, 5, ErrorMessage = "El puntaje de conectividad debe estar entre 1 y 5.")] int Connectivity,
    [Range(1, 5, ErrorMessage = "El puntaje de comercios y servicios debe estar entre 1 y 5.")] int Commerce,
    [Range(1, 5, ErrorMessage = "El puntaje de espacios verdes debe estar entre 1 y 5.")] int GreenSpaces);
