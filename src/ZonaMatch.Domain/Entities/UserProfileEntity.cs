namespace ZonaMatch.Domain.Entities;

public class UserProfileEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Email { get; set; }
    public string? Name { get; set; }
    public string HouseholdType { get; set; } = "individual";
    public int MaxTravelTimeMinutes { get; set; } = 45;
    
    // Almacenado como JSON en PostgreSQL
    public string WeightsJson { get; set; } = "{\"salud\":2,\"educacion\":2,\"abastecimiento\":2,\"espacios_verdes\":2,\"movilidad\":3,\"clima\":1}";

    public ICollection<UserDestinationEntity> Destinations { get; set; } = new List<UserDestinationEntity>();
    public ICollection<ZoneEvaluationEntity> Evaluations { get; set; } = new List<ZoneEvaluationEntity>();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
