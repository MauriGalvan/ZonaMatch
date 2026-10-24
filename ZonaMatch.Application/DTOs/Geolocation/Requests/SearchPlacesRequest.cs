using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.Geolocation.Requests
{
    public sealed class SearchPlacesRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Search { get; set; } = string.Empty;
    }
}
