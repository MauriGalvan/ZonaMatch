using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.DTOs.Scoring.Responses;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Application.UseCases.Scoring
{
    // Shared housing validation and market response mapping for individual and group scoring.
    public static class HousingScoring
    {
        public const decimal MaximumPurchasePriceUsd = 100_000_000;
        public const int MinimumSurfaceM2 = 10;
        public const int MaximumSurfaceM2 = 2000;

        // Without a surface (or when it is 0), return each zone's average prices.
        public static int? ValidateSurface(int? surfaceM2)
        {
            if (surfaceM2 is null or 0)
                return null;

            if (surfaceM2 is < MinimumSurfaceM2 or > MaximumSurfaceM2)
                throw new BadRequestException("La superficie debe estar entre 10 y 2.000 m².");

            return surfaceM2;
        }

        // Operation defaults to rent for profiles saved before purchase was available.
        public static (string Operation, decimal? MaximumPurchasePriceUsd, string? HousingType) Validate(
            string? operation, decimal? maximumPurchasePriceUsd, string? housingType)
        {
            var requestedOperation = string.IsNullOrWhiteSpace(operation) ? HousingOperations.Rent : operation.Trim();
            if (!HousingOperations.IsValid(requestedOperation))
                throw new BadRequestException("La operación debe ser alquiler o compra.");

            if (maximumPurchasePriceUsd is < 0 or > MaximumPurchasePriceUsd)
                throw new BadRequestException("El precio máximo de compra no es válido.");

            var requestedHousingType = string.IsNullOrWhiteSpace(housingType) || housingType.Trim() == HousingTypes.All ? null : housingType.Trim();
            if (requestedHousingType is not null && !HousingTypes.IsValid(requestedHousingType))
                throw new BadRequestException("El tipo de vivienda debe ser departamento, casa o PH.");

            var purchaseLimit = requestedOperation == HousingOperations.Purchase && maximumPurchasePriceUsd > 0 ? maximumPurchasePriceUsd : null;
            return (requestedOperation, purchaseLimit, requestedHousingType);
        }

        // Null is returned when no rent data exists for the requested type and zone.
        public static RentResponse? Rent(ZoneProfile zone, string? housingType, int? surfaceM2 = null)
        {
            var market = zone.GetMarket(housingType);
            return market.AlquilerPromedio is { } average
                ? new RentResponse(
                    average,
                    market.PropiedadesEnAlquiler ?? 0,
                    market.ExpensasPromedio,
                    market.MonthlyRentCostForArea(surfaceM2),
                    market.AlquilerM2,
                    market.EstimatesRent(surfaceM2) ? surfaceM2 : null,
                    market.EstimatesRent(surfaceM2) ? market.RentForArea(surfaceM2) : null)
                : null;
        }

        // Null is returned when no sale data exists for the requested type and zone.
        public static SaleResponse? Sale(ZoneProfile zone, string? housingType, int? surfaceM2 = null)
        {
            var market = zone.GetMarket(housingType);
            return market.VentaPromedioUsd is { } average
                ? new SaleResponse(
                    average,
                    market.PropiedadesEnVenta ?? 0,
                    market.ExpensasPromedio,
                    market.VentaM2Usd,
                    market.EstimatesSale(surfaceM2) ? surfaceM2 : null,
                    market.EstimatesSale(surfaceM2) ? market.SalePriceUsdForArea(surfaceM2) : null)
                : null;
        }
    }
}
