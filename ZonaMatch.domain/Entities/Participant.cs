using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Domain.Entities;

public class Participant
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    // Foreign key to the group.
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;

    // Foreign key to the participant's user record.
    // EF Core discovers this relationship without changes to the User class.
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public required GroupRole Role { get; set; }
}
