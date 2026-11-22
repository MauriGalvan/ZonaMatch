using System.Security.Cryptography;
using System.Text.Json;
using ZonaMatch.Application.DTOs.Scoring.Requests;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Nps;

public sealed class RecordCompletedZoneAnalysis(INpsRepository repository, TimeProvider clock)
{
    public Task ExecuteAsync(Guid userId, CalculateScoresRequest request, CancellationToken cancellationToken = default)
    {
        // Dictionary and zone selection order do not change the analysis. Store only its hash, not the places.
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Criteria = request.Criteria.OrderBy(pair => pair.Key),
            AddedZones = request.AddedZones.Order(), request.InitialZone, request.Places,
            request.MaximumRent, request.Operation, request.MaximumPurchasePriceUsd, request.HousingType,
            request.AreaM2, request.Rooms, request.CreditEligible,
            Mobility = request.Mobility.Order(), ZoneOrigins = request.ZoneOrigins.OrderBy(point => point.Slug)
        });
        return repository.RecordCompletionAsync(userId, Convert.ToHexString(SHA256.HashData(payload)), clock.GetUtcNow(), cancellationToken);
    }
}
