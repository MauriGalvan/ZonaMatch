using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface ITokenService
    {
        // Issues a signed access token for the user and returns it with its expiration (UTC)
        (string Token, DateTimeOffset ExpiraEn) Generar(Usuario usuario);
    }
}
