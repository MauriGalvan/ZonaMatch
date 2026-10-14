using System;
using System.Collections.Generic;
using System.Text;

namespace ZonaMatch.Application.DTOs.GroupInvitation;

public class CreateInvitationRequest
{
    public Guid GroupId { get; set; }
    public string Email { get; set; } = string.Empty;
}
