using System;
using System.Collections.Generic;
using System.Text;

namespace ZonaMatch.Application.DTOs.Group;

public class GroupListResponse
{
    public ICollection<GroupResponse> Groups { get; set; }
}
