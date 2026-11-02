using System;
using System.Collections.Generic;
using System.Text;

namespace ZonaMatch.Application.DTOs.Group.Responses;

public class GroupListResponse
{
    public ICollection<GroupResponse> Groups { get; set; }
}
