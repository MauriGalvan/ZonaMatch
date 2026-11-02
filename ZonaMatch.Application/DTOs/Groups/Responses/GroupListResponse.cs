using System;
using System.Collections.Generic;
using System.Text;

namespace ZonaMatch.Application.DTOs.Groups.Responses;

public class GroupListResponse
{
    public ICollection<GroupResponse> Groups { get; set; }
}
