namespace ZonaMatch.Application.UseCases.Scoring
{
    // Visitors can score a few places; signed-in users can score places for their full household.
    public static class ScoringPlaceLimits
    {
        public const int Guest = 3;
        public const int AuthenticatedUser = 15;

        public static string GuestMessage => $"Como visitante podés medir hasta {Guest} lugares. Creá tu cuenta para sumar hasta {AuthenticatedUser}.";
        public static string AuthenticatedUserMessage => $"Se pueden medir hasta {AuthenticatedUser} lugares.";
    }
}
