namespace ZonaMatch.Application.DTOs
{
    // The frontend sends AccessToken as "Authorization: Bearer <token>" and should ask for a new login after ExpiraEn
    public record LoginResponseDto(string AccessToken, DateTimeOffset ExpiraEn, UsuarioDto Usuario);
}
