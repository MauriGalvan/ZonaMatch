using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ZonaMatch.Application.DTOs.Group;

public class UpdateGroupRequest
{
    [Required(ErrorMessage = "El nombre del grupo es obligatorio.")]
    [StringLength(25, MinimumLength = 1, ErrorMessage = "El nombre debe tener entre 1 y 25 caracteres.")]
    public string Name { get; set; } = string.Empty;
}
