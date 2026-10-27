using System.Globalization;
using ZonaMatch.Application.DTOs.Group;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Application.UseCases.Groups;

public sealed class GetGroupDetails
{
    private readonly IGroupRepository _groups;
    private readonly IPerfilRepository _profiles;
    private readonly ICriterioRepository _criteria;

    public GetGroupDetails(
        IGroupRepository groups,
        IPerfilRepository profiles,
        ICriterioRepository criteria)
    {
        _groups = groups;
        _profiles = profiles;
        _criteria = criteria;
    }

    public async Task<GroupDetailsResponse> ExecuteAsync(
        Guid groupId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var group = await _groups.GetDetailsByIdAsync(groupId, cancellationToken)
            ?? throw new NotFoundException("No se encontró el grupo.");

        if (!group.Participants.Any(participant => participant.UserId == currentUserId))
            throw new ForbiddenException("No pertenecés a este grupo.");

        var participantUserIds = group.Participants
            .Select(participant => participant.UserId)
            .Distinct()
            .ToArray();
        var profiles = await _profiles.ObtenerDeUsuariosAsync(participantUserIds, cancellationToken);
        var profilesByUserId = profiles.ToDictionary(profile => profile.UsuarioId);
        var catalog = await _criteria.ListarAsync(cancellationToken);
        var criteria = catalog
            .Where(criterion => criterion.Activo)
            .Select(criterion => new GroupCriterionResponse(criterion.Id, criterion.Nombre))
            .ToArray();

        var participants = group.Participants
            .OrderBy(participant => participant.Role == GroupRole.Owner ? 0 : 1)
            .ThenBy(participant => participant.User.Email)
            .Select(participant =>
            {
                profilesByUserId.TryGetValue(participant.UserId, out var profile);
                var preferences = profile?.Criterios.ToDictionary(
                    criterion => criterion.CriterioId,
                    criterion => (string?)FormatPriority(criterion.Prioridad),
                    StringComparer.Ordinal)
                    ?? new Dictionary<string, string?>(StringComparer.Ordinal);
                var restrictionCount = CountRestrictions(profile);

                return new GroupParticipantResponse(
                    participant.Id,
                    participant.UserId,
                    participant.User.Email,
                    participant.UserId == currentUserId,
                    GetInitials(participant.User.Email),
                    participant.Role == GroupRole.Owner ? "Organizador" : "Invitado",
                    new GroupParticipantStatsResponse(
                        preferences.Count,
                        profile?.Puntos.Count ?? 0,
                        restrictionCount),
                    preferences,
                    profile?.Puntos
                        .OrderBy(point => point.Orden)
                        .Select(point => new GroupParticipantPointResponse(
                            point.Id,
                            point.Nombre,
                            point.Direccion,
                            point.Latitude,
                            point.Longitude,
                            point.Tipo,
                            point.Prioridad,
                            point.TiempoMax))
                        .ToArray() ?? []);
            })
            .ToArray();

        return new GroupDetailsResponse(
            group.Id,
            group.Name,
            group.CreatedAt,
            participants,
            criteria,
            BuildRestrictions(profiles, participants, profilesByUserId));
    }

    private static GroupRestrictionsResponse BuildRestrictions(
        IReadOnlyList<Contexto> profiles,
        IReadOnlyList<GroupParticipantResponse> participants,
        IReadOnlyDictionary<Guid, Contexto> profilesByUserId)
    {
        var housing = GroupHousingSuggestion.From(profiles);
        return new GroupRestrictionsResponse(
            housing.MonthlyBudget,
            GetTravelTimesText(profiles),
            GetPoiText(participants, profilesByUserId),
            housing.Operation,
            housing.MaxPurchaseUsd,
            housing.PropertyType,
            housing.SurfaceM2);
    }

    private static int CountRestrictions(Contexto? profile)
    {
        if (profile is null)
            return 0;

        var rentLimit = string.IsNullOrWhiteSpace(profile.AlquilerMaximo) ? 0 : 1;
        var purchaseLimit = profile.CompraMaximaUsd is > 0 ? 1 : 0;
        var mobility = profile.Movilidad is { Length: > 0 } ? 1 : 0;
        var pets = profile.Mascotas is { Length: > 0 } ? 1 : 0;
        var travelTimeLimits = profile.Puntos.Count(point => !string.IsNullOrWhiteSpace(point.TiempoMax));
        return rentLimit + purchaseLimit + mobility + pets + travelTimeLimits;
    }

    private static string GetTravelTimesText(IReadOnlyList<Contexto> profiles)
    {
        var hasTravelTimeLimits = profiles.Any(profile =>
            profile.Puntos.Any(point => !string.IsNullOrWhiteSpace(point.TiempoMax)));

        return hasTravelTimeLimits
            ? "Cada uno mantiene sus tiempos máximos"
            : "Sin tiempos máximos configurados";
    }

    private static string GetPoiText(
        IReadOnlyList<GroupParticipantResponse> participants,
        IReadOnlyDictionary<Guid, Contexto> profilesByUserId)
    {
        var summaries = participants
            .Select(participant => new
            {
                participant.UserId,
                participant.Name,
                Count = profilesByUserId.TryGetValue(participant.UserId, out var profile) ? profile.Puntos.Count : 0,
            })
            .Where(summary => summary.Count > 0)
            .ToArray();

        var total = summaries.Sum(summary => summary.Count);
        if (total == 0)
            return "0";

        var breakdown = summaries
            .Select(summary => $"{summary.Count} de {summary.Name}")
            .ToArray();

        return $"{total} ({JoinInSpanish(breakdown)})";
    }

    private static string JoinInSpanish(IReadOnlyList<string> values) => values.Count switch
    {
        0 => string.Empty,
        1 => values[0],
        2 => $"{values[0]} y {values[1]}",
        _ => $"{string.Join(", ", values.Take(values.Count - 1))} y {values[^1]}",
    };

    private static string GetInitials(string email)
    {
        var localPart = email.Split('@', 2)[0];
        var words = localPart.Split(['.', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2)
            return string.Concat(words.Take(2).Select(word => char.ToUpperInvariant(word[0])));

        return localPart.Length == 0 ? string.Empty : localPart[..Math.Min(2, localPart.Length)].ToUpperInvariant();
    }

    private static string FormatPriority(string priority) => priority.ToLowerInvariant() switch
    {
        "alta" => "Alta",
        "media" => "Media",
        "baja" => "Baja",
        _ => priority,
    };
}
