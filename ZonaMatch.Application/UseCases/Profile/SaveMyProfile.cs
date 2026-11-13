using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Profile.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Services;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Profile;

// Creates the user's profile during onboarding or replaces it. Saved analyses keep their own criteria snapshot.
public sealed class SaveMyProfile
{
    private readonly IProfileRepository _profiles;
    private readonly ICriterionRepository _criteria;
    private readonly TimeProvider _clock;

    public SaveMyProfile(IProfileRepository profiles, ICriterionRepository criteria, TimeProvider clock)
    {
        _profiles = profiles;
        _criteria = criteria;
        _clock = clock;
    }

    public async Task<ProfileResponse> ExecuteAsync(
        Guid userId, AnalysisCriteriaRequest request, CancellationToken cancellationToken = default)
    {
        // Investor criteria belong to the investor profile only
        var catalog = (await _criteria.ListAsync(cancellationToken)).Where(criterion => criterion.ForHousehold).ToList();
        var now = _clock.GetUtcNow();
        var profile = await _profiles.GetByUserAsync(userId, cancellationToken);
        var isNewProfile = profile is null;

        profile ??= new Context { UserId = userId, Type = ContextType.Profile, CreatedAt = now };

        // Validate the full request before adding the profile so invalid input cannot leave partial data.
        AnalysisCriteriaBuilder.Apply(profile, request, catalog);
        profile.UpdatedAt = now;

        if (isNewProfile)
            _profiles.Add(profile);

        await _profiles.SaveChangesAsync(cancellationToken);

        return ProfileMapper.ToResponse(profile);
    }
}
