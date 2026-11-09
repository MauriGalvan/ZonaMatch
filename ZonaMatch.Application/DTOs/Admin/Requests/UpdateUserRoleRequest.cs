using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.Admin.Requests;

// PUT /api/v1/admin/users/{id}/role
public sealed record UpdateUserRoleRequest(
    [Required(ErrorMessage = "Indicá si el usuario es administrador.")]
    bool? IsAdmin);
