namespace ZonaMatch.Application.UseCases.Scoring
{
    // Visitors can score a few places; signed-in users can score places for their full household.
    public static class ScoringPlaceLimits
    {
        public const int Guest = 3;
        public const int AuthenticatedUser = 15;

        public const string GuestMessage = "Como visitante podés medir hasta 3 lugares. Creá tu cuenta para sumar hasta 15.";
        public const string AuthenticatedUserMessage = "Se pueden medir hasta 15 lugares.";
    }
}
