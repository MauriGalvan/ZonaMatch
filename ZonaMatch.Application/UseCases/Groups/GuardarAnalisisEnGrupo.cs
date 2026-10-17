using System.Text.Json;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Group;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.AnalisisGuardados;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Groups;

public sealed class GuardarAnalisisEnGrupo
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CalcularPuntajeEnGrupo _calculateRanking;
    private readonly IAnalisisRepository _analyses;
    private readonly IPerfilRepository _profiles;
    private readonly IZonaRepository _zones;
    private readonly TimeProvider _clock;

    public GuardarAnalisisEnGrupo(
        CalcularPuntajeEnGrupo calculateRanking,
        IAnalisisRepository analyses,
        IPerfilRepository profiles,
        IZonaRepository zones,
        TimeProvider clock)
    {
        _calculateRanking = calculateRanking;
        _analyses = analyses;
        _profiles = profiles;
        _zones = zones;
        _clock = clock;
    }

    public async Task<AnalisisDto> EjecutarAsync(
        Guid groupId,
        Guid currentUserId,
        GuardarAnalisisEnGrupoRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Nombre.Trim();
        if (name.Length == 0)
            throw new DatosInvalidosException("El nombre es obligatorio.");

        var now = _clock.GetUtcNow();
        var ranked = await _calculateRanking.EjecutarAsync(
            groupId,
            currentUserId,
            new CalcularPuntajeEnGrupoRequest
            {
                Zonas = request.Zonas.Select(zone => zone.Slug).ToList(),
                PesosParticipantes = request.PesosParticipantes,
                AlquilerMaximo = request.AlquilerMaximo,
            },
            cancellationToken);

        var profile = await _profiles.ObtenerDeUsuarioAsync(currentUserId, cancellationToken);
        var context = profile?.CopiarParaAnalisis(now) ?? new Contexto
        {
            UsuarioId = currentUserId,
            Tipo = TipoContexto.Analisis,
            Personas = 1,
            FechaCreacion = now,
            FechaActualizacion = now,
        };
        var zones = await AnalisisMapeo.ArmarZonasAsync(request.Zonas, _zones, cancellationToken);

        var analysis = new Analisis
        {
            UsuarioId = currentUserId,
            Nombre = name,
            Contexto = context,
            FechaCreacion = now,
            FechaActualizacion = now,
            Zonas = zones,
            PuntajesEnGrupoJson = JsonSerializer.Serialize(ranked, JsonOptions),
        };

        _analyses.Agregar(analysis);
        await _analyses.GuardarCambiosAsync(cancellationToken);

        return AnalisisMapeo.ADto(analysis);
    }
}
