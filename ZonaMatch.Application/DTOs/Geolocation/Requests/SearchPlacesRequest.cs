using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.Geolocation.Requests
{
    public sealed class SearchPlacesRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Search { get; set; } = string.Empty;

        // "nominatim" or "photon". Only honored when PlaceSearch:AllowProviderOverride is true (comparison in dev).
        [StringLength(20)]
        public string? Provider { get; set; }
    }
}
