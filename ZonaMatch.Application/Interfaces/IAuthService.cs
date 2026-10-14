using ZonaMatch.Application.DTOs;

namespace ZonaMatch.Application.Interfaces
{
    public interface IAuthService
    {
        // Throws EmailYaRegistradoException when the email is already taken
        Task<UsuarioDto> RegistrarAsync(RegistroUsuarioDto request, CancellationToken cancellationToken = default);

        // Throws CredencialesInvalidasException when the email or the password is wrong
        Task<LoginResponseDto> LoginAsync(LoginDto request, CancellationToken cancellationToken = default);
    }
}
