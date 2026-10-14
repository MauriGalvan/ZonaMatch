namespace ZonaMatch.Application.DTOs
{
    // Public representation of a user. Never includes the password hash.
    public record UsuarioDto(Guid Id, string Email, DateOnly FechaNacimiento);
}
