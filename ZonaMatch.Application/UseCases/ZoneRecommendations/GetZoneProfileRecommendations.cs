using ZonaMatch.Application.DTOs.ZoneRecommendations;
using ZonaMatch.Application.DTOs.ZoneRecommendations.Requests;
using ZonaMatch.Application.DTOs.ZoneRecommendations.Responses;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.ZoneRecommendations;

public sealed class GetZoneProfileRecommendations(IPuntoInteresRepository pointOfInterestRepository)
{
    private const int MaximumPlacesPerCategory = 100;

    public async Task<ZoneProfileRecommendationsResponse> ExecuteAsync(
        string slug,
        GetZoneProfileRecommendationsRequest request,
        CancellationToken cancellationToken = default)
    {
        var categories = ResolveCategories(request);
        if (categories.Count == 0)
            return new ZoneProfileRecommendationsResponse([], false);

        var places = await pointOfInterestRepository.GetProfileRecommendationsInZoneAsync(
            slug,
            categories,
            MaximumPlacesPerCategory,
            cancellationToken);

        return new ZoneProfileRecommendationsResponse(
            places.Select(place => new ProfileRecommendationResponse(
                place.Id,
                place.Name,
                place.Category,
                place.Latitude,
                place.Longitude,
                place.DistanceMeters)).ToList(),
            places.Any(place => place.IsTruncated));
    }

    private static IReadOnlyList<string> ResolveCategories(GetZoneProfileRecommendationsRequest request)
    {
        var categories = new HashSet<string>(StringComparer.Ordinal);

        foreach (var ageRange in SplitValues(request.AgeRanges))
        {
            var normalizedRange = ageRange
                .Replace("años", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace('–', '-')
                .Replace('—', '-')
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .ToLowerInvariant();

            var category = normalizedRange switch
            {
                "0-2" or "0-1" => ProfileRecommendationCategories.MaternalSchool,
                "3-5" or "2-5" => ProfileRecommendationCategories.Kindergarten,
                "6-11" => ProfileRecommendationCategories.PrimarySchool,
                "12-17" => ProfileRecommendationCategories.SecondarySchool,
                _ => null,
            };

            if (category is not null)
                categories.Add(category);
        }

        if (request.HasPets)
            categories.Add(ProfileRecommendationCategories.Veterinary);

        if (request.HasVehicle)
            categories.Add(ProfileRecommendationCategories.CarRepair);
        if (request.HasBicycle)
            categories.Add(ProfileRecommendationCategories.BicycleShop);

        return categories.ToArray();
    }

    private static IEnumerable<string> SplitValues(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => !string.IsNullOrWhiteSpace(item));
}
