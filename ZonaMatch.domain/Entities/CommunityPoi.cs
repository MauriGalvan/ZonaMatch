using NetTopologySuite.Geometries;

namespace ZonaMatch.Domain.Entities
{
    // A point of interest as the community left it (table app.community_pois): a point added by users, or the
    // approved changes to an OpenStreetMap point. Written when a contribution is approved; the map queries apply it
    // over the OSM import.
    public class CommunityPoi
    {
        // "a<contribution id>" for a point added by users, or the OSM id ("n123", "w45", "r6") of a changed point
        public string Id { get; set; } = string.Empty;

        // Added by users (true) or a change to an OSM point (false)
        public bool IsNew { get; set; }

        public string ZoneSlug { get; set; } = string.Empty;

        // A new point has all three. An OSM point only the ones that changed: null keeps the OSM value.
        public string? Name { get; set; }
        public string? Category { get; set; }
        public Point? Location { get; set; }

        // The place closed: it is no longer shown
        public bool Removed { get; set; }

        // The last approved contribution applied to the point; null if it was deleted with its author account
        public Guid? LastContributionId { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        public static string NewPointId(Guid contributionId) => "a" + contributionId;

        public static CommunityPoi FromNewPoint(Aporte contribution, DateTimeOffset now) =>
            new()
            {
                Id = NewPointId(contribution.Id),
                IsNew = true,
                ZoneSlug = contribution.ZonaSlug,
                Name = contribution.Nombre,
                Category = contribution.Categoria,
                Location = contribution.Ubicacion,
                LastContributionId = contribution.Id,
                UpdatedAt = now
            };

        // An OSM point the community has not changed yet
        public static CommunityPoi ForOsmPoint(string poiId, string zoneSlug) =>
            new() { Id = poiId, IsNew = false, ZoneSlug = zoneSlug };

        // Applies an approved correction. "Otro" is informative only and changes nothing.
        public void ApplyCorrection(Aporte correction, DateTimeOffset now)
        {
            switch (correction.Motivo)
            {
                case MotivoCorreccion.Cerro:
                    Removed = true;
                    break;
                case MotivoCorreccion.Nombre:
                    Name = correction.Nombre;
                    break;
                case MotivoCorreccion.Categoria:
                    Category = correction.Categoria;
                    break;
                case MotivoCorreccion.Ubicacion:
                    Location = correction.Ubicacion;
                    break;
                default:
                    return;
            }

            LastContributionId = correction.Id;
            UpdatedAt = now;
        }
    }
}
