using System.Text.Json.Serialization;
using NetTopologySuite.Geometries;

namespace ZonaMatch.Domain.Entities
{
    public enum ContributionType
    {
        // A place missing from the map.
        [JsonStringEnumMemberName("punto-nuevo")]
        NewPoint,

        // Something is wrong with a place on the map.
        [JsonStringEnumMemberName("correccion")]
        Correction
    }

    public enum ContributionStatus
    {
        [JsonStringEnumMemberName("pendiente")]
        Pending,

        [JsonStringEnumMemberName("aprobado")]
        Approved,

        [JsonStringEnumMemberName("rechazado")]
        Rejected
    }

    public enum CorrectionReason
    {
        [JsonStringEnumMemberName("cerro")]
        Closed,

        [JsonStringEnumMemberName("nombre")]
        Name,

        [JsonStringEnumMemberName("ubicacion")]
        Location,

        [JsonStringEnumMemberName("categoria")]
        Category,

        [JsonStringEnumMemberName("otro")]
        Other
    }

    // A community contribution to a zone map (app.aportes table). It remains pending until other community
    // members validate it; approved points and corrections are applied to points of interest.
    public class Contribution
    {
        // Difference between confirmations and rejections needed to resolve a pending contribution.
        public const int VoteDifferenceToResolve = 3;

        public Guid Id { get; set; } = Guid.CreateVersion7();

        public string ZoneSlug { get; set; } = string.Empty;

        public Guid UserId { get; set; }
        public User? User { get; set; }

        public ContributionType Type { get; set; }
        public ContributionStatus Status { get; set; } = ContributionStatus.Pending;

        // For a new point, its data. For a correction, the proposed value (Name, Category, or Location, based on Reason).
        public string? Name { get; set; }
        public string? Category { get; set; }
        public Point? Location { get; set; }

        // Used only for a new point.
        public string? OpeningHours { get; set; }
        public string? Address { get; set; }
        public bool LivesOrWorksInZone { get; set; }

        // Used only for a correction: the corrected place (an OSM ID such as "n123", or "a<id>" for a community point).
        public string? PointOfInterestId { get; set; }
        public string? PointOfInterestName { get; set; }
        public CorrectionReason? Reason { get; set; }
        public string? Comment { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ResolvedAt { get; set; }

        // Site administrator who resolved it directly; null when the community votes resolved it.
        public Guid? ResolvedByUserId { get; set; }

        public List<ContributionVote> Votes { get; set; } = [];

        public int Confirmations => Votes.Count(vote => vote.Confirms);
        public int Rejections => Votes.Count(vote => !vote.Confirms);

        // Record or change a user's vote and resolve the contribution when the vote threshold is reached.
        // The caller verifies that the contribution is pending and the voter is not its author.
        public void RecordVote(Guid userId, bool confirms, DateTimeOffset timestamp)
        {
            var existingVote = Votes.FirstOrDefault(vote => vote.UserId == userId);
            if (existingVote is null)
                Votes.Add(new ContributionVote { ContributionId = Id, UserId = userId, Confirms = confirms, CreatedAt = timestamp });
            else
            {
                existingVote.Confirms = confirms;
                existingVote.CreatedAt = timestamp;
            }

            var difference = Confirmations - Rejections;
            if (Math.Abs(difference) >= VoteDifferenceToResolve)
            {
                Status = difference > 0 ? ContributionStatus.Approved : ContributionStatus.Rejected;
                ResolvedAt = timestamp;
            }
        }

        // A site administrator approves or rejects it without waiting for the votes.
        // The caller checks that the contribution is pending.
        public void Resolve(Guid administratorId, bool approve, DateTimeOffset now)
        {
            Status = approve ? ContributionStatus.Approved : ContributionStatus.Rejected;
            ResolvedAt = now;
            ResolvedByUserId = administratorId;
        }
    }

    // A community member's vote on a contribution (app.aportes_validaciones table).
    public class ContributionVote
    {
        public Guid ContributionId { get; set; }
        public Guid UserId { get; set; }
        public bool Confirms { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
