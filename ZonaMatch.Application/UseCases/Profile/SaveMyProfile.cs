using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Profile.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Services;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Profile;

// Creates the user's profile during onboarding or replaces it. Saved analyses keep their own criteria snapshot.
public sealed class SaveMyProfile
{
    private readonly IPerfilRepository _profiles;
    private readonly ICriterioRepository _criteria;
    private readonly TimeProvider _clock;

    public SaveMyProfile(IPerfilRepository profiles, ICriterioRepository criteria, TimeProvider clock)
    {
        _profiles = profiles;
        _criteria = criteria;
        _clock = clock;
    }

    public async Task<ProfileResponse> ExecuteAsync(
        Guid userId, AnalysisCriteriaRequest request, CancellationToken cancellationToken = default)
    {
        var catalog = await _criteria.ListarAsync(cancellationToken);
        var now = _clock.GetUtcNow();
        var profile = await _profiles.ObtenerDeUsuarioAsync(userId, cancellationToken);
        var isNewProfile = profile is null;

        profile ??= new Context { UsuarioId = userId, Tipo = ContextType.Perfil, FechaCreacion = now };

        // Validate the full request before adding the profile so invalid input cannot leave partial data.
        AnalysisCriteriaBuilder.Apply(profile, request, catalog);
        profile.FechaActualizacion = now;

        if (isNewProfile)
            _profiles.Agregar(profile);

        await _profiles.GuardarCambiosAsync(cancellationToken);

        return ProfileMapper.ToResponse(profile);
    }
}
