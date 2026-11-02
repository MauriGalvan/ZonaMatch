using System.Text.Json.Serialization;
using ZonaMatch.Application.DTOs.GroupAnalysis.Responses;

namespace ZonaMatch.Application.DTOs.Analysis.Responses;

public sealed record LegacyComparedAnalysisResponse(
    Guid Id,
    [property: JsonPropertyName("nombre")] string Name,
    [property: JsonPropertyName("modoViaje")] string TravelMode,
    [property: JsonPropertyName("zonas")] IReadOnlyList<LegacyScoredZoneResponse> Zones,
    [property: JsonPropertyName("puntajesEnGrupo")] GroupRankingResponse? GroupScores = null);
