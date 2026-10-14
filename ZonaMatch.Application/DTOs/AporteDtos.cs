using System.ComponentModel.DataAnnotations;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.DTOs
{
    // GET /Zonas/{slug}/aportes/resumen. Total cuenta los aportes aprobados y los pendientes.
    public record ResumenAportesDto(int Total, int Aprobados, int Pendientes);

    // Un aporte tal como lo ven los otros vecinos para validarlo.
    // MiVoto: true confirmado, false rechazado, null sin votar (o pedido anonimo).
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
        // Punto nuevo: sus datos. Correccion: el valor propuesto.
        string? Nombre,
        string? Categoria,
        CoordenadaDto? Ubicacion,
        string? Horario,
        string? Direccion,
        bool ViveOTrabajaEnZona,
        // Solo correccion
        string? PuntoInteresId,
        string? PuntoInteresNombre,
        MotivoCorreccion? Motivo,
        string? Comentario);

    // POST /Zonas/{slug}/aportes/puntos. El punto tiene que estar dentro de la zona.
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

    // POST /Zonas/{slug}/aportes/correcciones. Lo obligatorio depende del Motivo:
    // nombre -> NombrePropuesto, categoria -> CategoriaPropuesta, ubicacion -> Latitud/Longitud dentro de la zona,
    // otro -> Comentario. Cerro no pide nada mas.
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
