using System.ComponentModel.DataAnnotations;
using ZonaMatch.Application.DTOs;

namespace ZonaMatch.Application.DTOs.Group;

public sealed class CalcularPuntajeEnGrupoRequest
{
    [MaxLength(5, ErrorMessage = "Se pueden agregar hasta 5 zonas después del ranking.")]
    public List<string> ZonasAgregadas { get; set; } = [];

    [Required]
    [MinLength(1, ErrorMessage = "Indicá el peso de cada participante.")]
    [MaxLength(5, ErrorMessage = "Un grupo puede tener hasta 5 participantes.")]
    public List<PesoParticipanteRequest> PesosParticipantes { get; set; } = [];

    // Tope común editable desde el panel de ranking. Null o cero significa sin tope.
    [Range(typeof(decimal), "0", "999999999999")]
    public decimal? AlquilerMaximo { get; set; }
}

public sealed class PesoParticipanteRequest
{
    [Required]
    public Guid ParticipantId { get; set; }

    [Range(typeof(decimal), "0", "100")]
    public decimal Peso { get; set; }
}

public sealed class GuardarAnalisisEnGrupoRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "El nombre debe tener entre 1 y 50 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Las zonas son obligatorias.")]
    [MinLength(1, ErrorMessage = "Elegí al menos una zona.")]
    [MaxLength(10, ErrorMessage = "Se pueden comparar hasta 10 zonas.")]
    public List<ZonaAnalisisRequest> Zonas { get; set; } = [];

    [MaxLength(5, ErrorMessage = "Se pueden agregar hasta 5 zonas después del ranking.")]
    public List<string> ZonasAgregadas { get; set; } = [];

    [Required]
    [MinLength(1, ErrorMessage = "Indicá el peso de cada participante.")]
    [MaxLength(5, ErrorMessage = "Un grupo puede tener hasta 5 participantes.")]
    public List<PesoParticipanteRequest> PesosParticipantes { get; set; } = [];

    [Range(typeof(decimal), "0", "999999999999")]
    public decimal? AlquilerMaximo { get; set; }
}

public sealed record PuntajesEnGrupoResponse(
    IReadOnlyList<ZonaPuntajeEnGrupoResponse> Zonas,
    IReadOnlyList<ParticipanteExcluidoEnGrupoResponse> ParticipantesExcluidos,
    IReadOnlyList<PuntoDeInteresEnGrupoResponse> PuntosParticipantes,
    decimal? AlquilerMaximo = null,
    IReadOnlyList<ZonaPuntajeEnGrupoResponse>? Cercanas = null);

public sealed record ZonaPuntajeEnGrupoResponse(
    string Slug,
    string Nombre,
    int? Puntaje,
    IReadOnlyList<ParticipantePuntajeEnGrupoResponse> Participantes,
    IReadOnlyList<ComponentePuntajeEnGrupoResponse>? Componentes = null,
    IReadOnlyList<LimitePuntajeEnGrupoResponse>? Limites = null,
    IReadOnlyList<ViajeParticipanteEnGrupoResponse>? Lugares = null,
    AlquilerZonaDto? Alquiler = null);

public sealed record ParticipantePuntajeEnGrupoResponse(
    Guid ParticipantId,
    string Nombre,
    decimal Peso,
    decimal PesoNormalizado,
    int? Puntaje);

public sealed record ParticipanteExcluidoEnGrupoResponse(
    Guid ParticipantId,
    string Nombre,
    string Motivo);

public sealed record PuntoDeInteresEnGrupoResponse(
    Guid Id,
    Guid ParticipantId,
    string ParticipanteNombre,
    string Nombre,
    string? Direccion,
    double? Latitude,
    double? Longitude,
    string? Tipo,
    string? Prioridad,
    string? TiempoMax);

public sealed record ComponentePuntajeEnGrupoResponse(
    string Criterio,
    int? Puntaje,
    string Detalle,
    IReadOnlyList<ComponenteParticipanteEnGrupoResponse> Participantes);

public sealed record ComponenteParticipanteEnGrupoResponse(
    Guid ParticipantId,
    string Nombre,
    decimal PesoNormalizado,
    string Prioridad,
    int? Puntaje,
    string Detalle);

public sealed record LimitePuntajeEnGrupoResponse(
    Guid ParticipantId,
    string ParticipanteNombre,
    decimal PesoNormalizado,
    string Tipo,
    bool? Cumple,
    string Detalle);

public sealed record ViajeParticipanteEnGrupoResponse(
    Guid Id,
    Guid ParticipantId,
    string ParticipanteNombre,
    string Nombre,
    string? Direccion,
    string? Tipo,
    string? Prioridad,
    string ModoViaje,
    int? Minutos,
    int MaximoMinutos,
    bool Estimado,
    int? Puntaje);
