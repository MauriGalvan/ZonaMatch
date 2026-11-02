using System;
using System.Collections.Generic;
using System.Text;

namespace ZonaMatch.Application.DTOs.GroupLinks.Responses;

public class GroupLinkDetailsResponse
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
}
