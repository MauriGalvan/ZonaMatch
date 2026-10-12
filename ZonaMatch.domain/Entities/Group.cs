using System;
using System.Collections.Generic;
using System.Text;

namespace ZonaMatch.Domain.Entities;

public class Group
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid InviteToken { get; set; } = Guid.NewGuid();
    public ICollection<Participant> Participants { get; set; } = new List<Participant>();
    public ICollection<GroupInvitation> Invitations { get; set; } = new List<GroupInvitation>();
}
