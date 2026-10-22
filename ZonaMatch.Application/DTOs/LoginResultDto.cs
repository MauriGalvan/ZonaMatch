namespace ZonaMatch.Application.DTOs
{
    // Internal application result used by the API to create the HttpOnly authentication cookie.
    public record LoginResultDto(string AccessToken, DateTimeOffset ExpiraEn, UsuarioDto Usuario);
}
