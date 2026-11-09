using System.Net.Http.Json;
using ZonaMatch.Application.DTOs.Review.Requests;

namespace ZonaMatch.Api.IntegrationTests.Support;

// Reviews wait for a site administrator before other users see them; tests use this to resolve them.
internal static class ReviewModeration
{
    public static Task<HttpResponseMessage> ResolveAsync(
        TestApiUser administrator, string zoneSlug, Guid reviewId, bool approve, string? reason = null) =>
        administrator.Client.PutAsJsonAsync(
            $"/api/v1/zones/{zoneSlug}/reviews/{reviewId}/resolution",
            new ResolveReviewRequest(approve, reason));
}
