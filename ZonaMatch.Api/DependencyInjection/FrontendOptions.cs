using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Api.DependencyInjection
{
    // Section "Frontend": base URL of the SPA for the links in emails (invitations, password resets, answers,
    // guest reports). Required outside development, where it falls back to the Vite dev server; in Azure it is the
    // environment variable Frontend__BaseUrl. A missing value would send links to localhost.
    public sealed class FrontendOptions
    {
        public const string SectionName = "Frontend";
        public const string DevelopmentBaseUrl = "http://localhost:5173";

        [Required(ErrorMessage = "Missing Frontend:BaseUrl (environment variable Frontend__BaseUrl): the base URL of the frontend used in email links.")]
        public string BaseUrl { get; set; } = string.Empty;
    }
}
