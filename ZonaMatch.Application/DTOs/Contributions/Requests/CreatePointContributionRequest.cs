using System.ComponentModel.DataAnnotations;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.DTOs.Contributions.Requests;

public sealed record CreatePointContributionRequest(
    [Required(ErrorMessage = "La categoria es obligatoria.")]
    string Category,
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(Longitudes.NombreLugar, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 120 caracteres.")]
    string Name,
    [StringLength(Longitudes.Horario, ErrorMessage = "El horario no puede superar los 120 caracteres.")]
    string? Schedule,
    [StringLength(Longitudes.Direccion, ErrorMessage = "La direccion no puede superar los 200 caracteres.")]
    string? Address,
    [Range(-90, 90, ErrorMessage = "La latitud debe estar entre -90 y 90.")]
    double Latitude,
    [Range(-180, 180, ErrorMessage = "La longitud debe estar entre -180 y 180.")]
    double Longitude,
    bool LivesOrWorksInZone);
