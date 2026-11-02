using ZonaMatch.Application.DTOs.Profile.Responses;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Profile;

// Returns the user's profile, or null when it has not been created yet.
public sealed class GetMyProfile
{
    private readonly IProfileRepository _profiles;

    public GetMyProfile(IProfileRepository profiles)
    {
        _profiles = profiles;
    }

    public async Task<ProfileResponse?> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await _profiles.GetByUserAsync(userId, cancellationToken);

        return profile is null ? null : ProfileMapper.ToResponse(profile);
    }
}
