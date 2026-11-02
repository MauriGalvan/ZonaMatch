using ZonaMatch.Application.DTOs.Profile.Responses;
using ZonaMatch.Application.Services;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Profile;

internal static class ProfileMapper
{
    public static ProfileResponse ToResponse(Context profile) =>
        new(
            AnalysisCriteriaBuilder.ToContextResponse(profile),
            AnalysisCriteriaBuilder.ToCriteriaDictionary(profile),
            AnalysisCriteriaBuilder.ToPointsResponse(profile),
            profile.FechaActualizacion);
}
