using System.ComponentModel.DataAnnotations;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.DTOs.Contribution.Requests;

public sealed record CreateContributionCorrectionRequest(
    [Required(ErrorMessage = "Indicá el lugar a corregir.")]
    [RegularExpression("^([nwr][0-9]{1,19}|a[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$",
        ErrorMessage = "El identificador del lugar no es válido.")]
    string PointOfInterestId,
    [Required(ErrorMessage = "Indicá el nombre del lugar a corregir.")]
    [StringLength(TextLimits.Address, ErrorMessage = "El nombre del lugar no puede superar los 200 caracteres.")]
    string PointOfInterestName,
    [Required(ErrorMessage = "Indicá qué hay que corregir.")]
    CorrectionReason? Reason,
    [StringLength(TextLimits.PlaceName, ErrorMessage = "El nombre propuesto no puede superar los 120 caracteres.")]
    string? ProposedName,
    string? ProposedCategory,
    [Range(-90, 90, ErrorMessage = "La latitud debe estar entre -90 y 90.")]
    double? Latitude,
    [Range(-180, 180, ErrorMessage = "La longitud debe estar entre -180 y 180.")]
    double? Longitude,
    [StringLength(TextLimits.Comment, ErrorMessage = "El comentario no puede superar los 500 caracteres.")]
    string? Comment);
