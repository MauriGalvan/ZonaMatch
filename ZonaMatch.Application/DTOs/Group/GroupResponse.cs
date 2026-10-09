using System;
using System.Collections.Generic;
using System.Text;

namespace ZonaMatch.Application.DTOs.Group;

public class GroupResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public DateTime CreatedAt { get; set; }
}
