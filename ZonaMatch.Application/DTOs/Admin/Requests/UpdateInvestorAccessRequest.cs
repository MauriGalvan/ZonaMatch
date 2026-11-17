using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.Admin.Requests;

// Body of PUT /api/v1/admin/users/{id}/investor-access: true grants the investor profile, false removes it.
public sealed record UpdateInvestorAccessRequest(
    [Required(ErrorMessage = "Indicá si el usuario tiene acceso al perfil de inversor.")]
    bool? Granted);
