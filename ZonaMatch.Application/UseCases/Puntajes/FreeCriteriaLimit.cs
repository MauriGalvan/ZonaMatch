using ZonaMatch.Application.Exceptions;

namespace ZonaMatch.Application.UseCases.Puntajes
{
    // Free accounts can weigh at most two criteria; paid accounts have no cap.
    // Only checked on new input, like LimitesObligatorios.
    public static class FreeCriteriaLimit
    {
        public const int Max = 2;

        public static void Validate(bool isFreeAccount, IEnumerable<string?> selectedCriteria)
        {
            if (!isFreeAccount)
                return;

            // The same criterion repeated counts once; blank entries are not criteria
            var distinctCount = selectedCriteria
                .Where(criterion => !string.IsNullOrWhiteSpace(criterion))
                .Select(criterion => criterion!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            if (distinctCount > Max)
                throw new BadRequestException($"Con la cuenta gratuita podés elegir hasta {Max} criterios.");
        }
    }
}