using ZonaMatch.Application.Exceptions;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Application.UseCases.Scoring
{
    // The engine needs every limit: without them it would fall back to defaults the user never chose.
    // Only checked on new input (scoring request, profile, new version); saved analyses recalculate as they are.
    public static class MandatoryScoringLimits
    {
        public static void Validate(string? monthlyBudget, string? operation, decimal? maximumPurchasePriceUsd, IEnumerable<string?> maximumTravelTimes)
        {
            // Buying, the monthly cap is only the building fee (expensas): optional, and 0 means no cap
            if (operation?.Trim() == HousingOperations.Purchase)
            {
                if (maximumPurchasePriceUsd is not > 0)
                    throw new BadRequestException("El precio máximo de compra es obligatorio.");
            }
            else if (!HasPositiveNumber(monthlyBudget))
                throw new BadRequestException("El gasto máximo por mes es obligatorio.");

            if (maximumTravelTimes.Any(time => !HasPositiveNumber(time)))
                throw new BadRequestException("El tiempo máximo de traslado es obligatorio en cada lugar.");
        }

        // "$ 800.000", "800000" or "45 min" -> true; blank or zero -> false
        private static bool HasPositiveNumber(string? value)
        {
            var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
            return decimal.TryParse(digits, out var number) && number > 0;
        }
    }
}
