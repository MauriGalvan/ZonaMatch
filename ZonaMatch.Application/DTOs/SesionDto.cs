namespace ZonaMatch.Application.DTOs
{
    // Identity of the caller, read from the validated JWT (no database access)
    public record SesionDto(Guid Id, string Email);
}
