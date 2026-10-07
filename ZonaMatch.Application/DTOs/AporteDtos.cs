using System.ComponentModel.DataAnnotations;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.DTOs
{
    // GET /Zonas/{slug}/aportes/resumen. Total counts approved and pending contributions.
    public record ResumenAportesDto(int Total, int Aprobados, int Pendientes);

    // A contribution as other neighbors see it to validate it.
    // MiVoto: true confirmed, false rejected, null not voted (or anonymous request).
    public record AporteDto(
        Guid Id,
        TipoAporte Tipo,
        EstadoAporte Estado,
        string Iniciales,
        DateTimeOffset Fecha,
        bool EsMio,
        bool? MiVoto,
        int Confirmaciones,
        int Rechazos,
        // New point: its data. Correction: the proposed value.
        string? Nombre,
        string? Categoria,
        CoordenadaDto? Ubicacion,
        string? Horario,
        string? Direccion,
        bool ViveOTrabajaEnZona,
        // Correction only
        string? PuntoInteresId,
        string? PuntoInteresNombre,
        MotivoCorreccion? Motivo,
        string? Comentario);

    // POST /Zonas/{slug}/aportes/puntos. The point must be inside the zone.
    public record CrearPuntoNuevoDto(
        [Required(ErrorMessage = "La categoria es obligatoria.")]
        string Categoria,
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(Longitudes.NombreLugar, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 120 caracteres.")]
        string Nombre,
        [StringLength(Longitudes.Horario, ErrorMessage = "El horario no puede superar los 120 caracteres.")]
        string? Horario,
        [StringLength(Longitudes.Direccion, ErrorMessage = "La direccion no puede superar los 200 caracteres.")]
        string? Direccion,
        [Range(-90, 90, ErrorMessage = "La latitud debe estar entre -90 y 90.")]
        double Latitud,
        [Range(-180, 180, ErrorMessage = "La longitud debe estar entre -180 y 180.")]
        double Longitud,
        bool ViveOTrabajaEnZona);

    // POST /Zonas/{slug}/aportes/correcciones. What is required depends on Motivo:
    // nombre -> NombrePropuesto, categoria -> CategoriaPropuesta, ubicacion -> Latitud/Longitud inside the zone,
    // otro -> Comentario. Cerro needs nothing else.
    public record CrearCorreccionDto(
        [Required(ErrorMessage = "Indica el lugar a corregir.")]
        [RegularExpression("^([nwr][0-9]{1,19}|a[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$",
            ErrorMessage = "El identificador del lugar no es valido.")]
        string PuntoInteresId,
        [Required(ErrorMessage = "Indica el nombre del lugar a corregir.")]
        [StringLength(Longitudes.Direccion, ErrorMessage = "El nombre del lugar no puede superar los 200 caracteres.")]
        string PuntoInteresNombre,
        [Required(ErrorMessage = "Indica que hay que corregir.")]
        MotivoCorreccion? Motivo,
        [StringLength(Longitudes.NombreLugar, ErrorMessage = "El nombre propuesto no puede superar los 120 caracteres.")]
        string? NombrePropuesto,
        string? CategoriaPropuesta,
        [Range(-90, 90, ErrorMessage = "La latitud debe estar entre -90 y 90.")]
        double? Latitud,
        [Range(-180, 180, ErrorMessage = "La longitud debe estar entre -180 y 180.")]
        double? Longitud,
        [StringLength(Longitudes.Comentario, ErrorMessage = "El comentario no puede superar los 500 caracteres.")]
        string? Comentario);

    // PUT /Zonas/{slug}/aportes/{id}/validacion
    public record ValidarAporteDto(
        [Required(ErrorMessage = "Indica si el aporte es correcto.")]
        bool? Confirma);
}
