using System.ComponentModel.DataAnnotations;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.DTOs
{
    // POST /Usuarios/me/puntos-interes. El punto se elige en el mapa; la direccion y el barrio los resuelve el
    // servidor a partir de las coordenadas. Tag es texto libre ("Trabajo", "Peluqueria"): si no existe, se crea.
    public record CrearPuntoInteresUsuarioDto(
        [Required(ErrorMessage = "El tag es obligatorio.")]
        [StringLength(Longitudes.TagPuntoUsuario, MinimumLength = Longitudes.TagPuntoUsuarioMinimo,
            ErrorMessage = "El tag debe tener entre 2 y 40 caracteres.")]
        string Tag,
        [StringLength(Longitudes.AliasPuntoUsuario, ErrorMessage = "El nombre no puede superar los 60 caracteres.")]
        string? Alias,
        [Range(-90, 90, ErrorMessage = "La latitud debe estar entre -90 y 90.")]
        double Latitud,
        [Range(-180, 180, ErrorMessage = "La longitud debe estar entre -180 y 180.")]
        double Longitud);

    // Un punto de interes personal tal como lo ve su dueño
    public record PuntoInteresUsuarioDto(
        Guid Id,
        string Tag,
        string? Alias,
        string? Direccion,
        string? Barrio,
        CoordenadaDto Ubicacion,
        DateTimeOffset FechaCreacion);
}
