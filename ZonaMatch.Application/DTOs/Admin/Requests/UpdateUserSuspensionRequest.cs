using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.Admin.Requests;

// Body of PUT /api/v1/admin/users/{id}/suspension: true suspends the account, false reactivates it.
public sealed record UpdateUserSuspensionRequest(
    [Required(ErrorMessage = "Indicá si la cuenta se suspende o se activa.")]
    bool? Suspended);
