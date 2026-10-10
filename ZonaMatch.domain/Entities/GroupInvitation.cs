using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Domain.Entities;

public class GroupInvitation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    // Relación con el Grupo
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;

    // Datos de la invitación por correo
    public string Email { get; set; } = string.Empty;
    public Guid Token { get; set; } = Guid.NewGuid();
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
