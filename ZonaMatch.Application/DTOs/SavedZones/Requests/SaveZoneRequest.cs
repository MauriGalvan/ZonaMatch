using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.SavedZones.Requests
{
    public class SaveZoneRequest
    {
        [Required, StringLength(150, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;

        [StringLength(300)]
        public string Subtitle { get; set; } = string.Empty;
    }
}
