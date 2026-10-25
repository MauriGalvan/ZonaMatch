namespace ZonaMatch.Application.DTOs.Routes.Requests
{
    // GET /api/v1/routes: trip from a zone to one of the user's places.
    // Mode: transporte-publico (default), auto, bici or a-pie, the same codes as the scoring.
    public sealed class RouteRequest
    {
        public double FromLatitude { get; set; }
        public double FromLongitude { get; set; }
        public double ToLatitude { get; set; }
        public double ToLongitude { get; set; }
        public string? Mode { get; set; }
    }
}
