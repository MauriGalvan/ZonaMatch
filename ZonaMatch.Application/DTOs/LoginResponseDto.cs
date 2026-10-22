namespace ZonaMatch.Application.DTOs
{
    // Public login response. The access token is delivered only in the HttpOnly authentication cookie.
    public record LoginResponseDto(DateTimeOffset ExpiraEn, UsuarioDto Usuario);
}
