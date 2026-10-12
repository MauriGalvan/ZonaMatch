using System;
using System.Collections.Generic;
using System.Text;

namespace ZonaMatch.Application.DTOs.GroupInvitation;

public class InvitationDetailsResponse
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
