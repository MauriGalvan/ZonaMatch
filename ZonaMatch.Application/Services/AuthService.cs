using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUsuarioRepository _usuarios;
        private readonly IPasswordHasher _passwordHasher;
        private readonly TimeProvider _timeProvider;
        private readonly ITokenService _tokens;

        public AuthService(
            IUsuarioRepository usuarios,
            IPasswordHasher passwordHasher,
            TimeProvider timeProvider,
            ITokenService tokens)
        {
            _usuarios = usuarios;
            _passwordHasher = passwordHasher;
            _timeProvider = timeProvider;
            _tokens = tokens;
        }

        public async Task<UsuarioDto> RegistrarAsync(RegistroUsuarioDto request, CancellationToken cancellationToken = default)
        {
            var email = NormalizarEmail(request.Email);

            if (await _usuarios.ExisteEmailAsync(email, cancellationToken))
                throw new EmailYaRegistradoException();

            var usuario = new Usuario
            {
                Email = email,
                PasswordHash = _passwordHasher.Hash(request.Password),
                FechaNacimiento = request.FechaNacimiento!.Value, // guaranteed by [Required] on the DTO
                FechaCreacion = _timeProvider.GetUtcNow()
            };

            await _usuarios.AddAsync(usuario, cancellationToken);

            return new UsuarioDto(usuario.Id, usuario.Email, usuario.FechaNacimiento);
        }

        public async Task<LoginResponseDto> LoginAsync(LoginDto request, CancellationToken cancellationToken = default)
        {
            var usuario = await _usuarios.ObtenerPorEmailAsync(NormalizarEmail(request.Email), cancellationToken);

            var passwordValida = _passwordHasher.Verify(usuario?.PasswordHash, request.Password);
            if (usuario is null || !passwordValida)
                throw new CredencialesInvalidasException();

            var (token, expiraEn) = _tokens.Generar(usuario);

            return new LoginResponseDto(token, expiraEn, new UsuarioDto(usuario.Id, usuario.Email, usuario.FechaNacimiento));
        }

        private static string NormalizarEmail(string email) => email.Trim().ToLowerInvariant();
    }
}
