 using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Services;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.Services
{
    public class AuthServiceTests
    {
        private static readonly DateTimeOffset Ahora = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeUsuarioRepository _usuarios = new();
        private readonly AuthService _sut;

        public AuthServiceTests()
        {
            _sut = new AuthService(_usuarios, new FakePasswordHasher(), new FixedTimeProvider(Ahora), new FakeTokenService());
        }

        private static RegistroUsuarioDto Request(string email = "ana@example.com", string password = "Secreta123") =>
            new(email, password, password, new DateOnly(1990, 5, 31));

        [Fact]
        public async Task Registrar_EmailNuevo_GuardaElUsuario()
        {
            var resultado = await _sut.RegistrarAsync(Request());

            var guardado = Assert.Single(_usuarios.Guardados);
            Assert.Equal(guardado.Id, resultado.Id);
            Assert.Equal("ana@example.com", resultado.Email);
            Assert.Equal(new DateOnly(1990, 5, 31), guardado.FechaNacimiento);
            Assert.Equal(Ahora, guardado.FechaCreacion);
        }

        [Fact]
        public async Task Registrar_NormalizaElEmail()
        {
            var resultado = await _sut.RegistrarAsync(Request(email: "  Ana@Example.COM "));

            Assert.Equal("ana@example.com", resultado.Email);
            Assert.Equal("ana@example.com", Assert.Single(_usuarios.Guardados).Email);
        }

        [Fact]
        public async Task Registrar_GuardaElHashYNuncaLaContrasenaEnTextoPlano()
        {
            await _sut.RegistrarAsync(Request(password: "Secreta123"));

            var guardado = Assert.Single(_usuarios.Guardados);
            Assert.Equal(FakePasswordHasher.Prefijo + "Secreta123", guardado.PasswordHash);
            Assert.NotEqual("Secreta123", guardado.PasswordHash);
        }

        [Fact]
        public async Task Registrar_EmailExistente_LanzaExcepcionYNoGuarda()
        {
            await _sut.RegistrarAsync(Request());

            await Assert.ThrowsAsync<EmailYaRegistradoException>(() => _sut.RegistrarAsync(Request()));

            Assert.Single(_usuarios.Guardados);
        }

        [Fact]
        public async Task Registrar_EmailExistenteConOtraCapitalizacion_LanzaExcepcion()
        {
            await _sut.RegistrarAsync(Request(email: "ana@example.com"));

            await Assert.ThrowsAsync<EmailYaRegistradoException>(
                () => _sut.RegistrarAsync(Request(email: "ANA@example.com ")));
        }

        [Fact]
        public async Task Registrar_ConflictoConcurrenteEnElRepositorio_PropagaLaExcepcion()
        {
            // Two requests passed the ExisteEmailAsync check; the unique index rejects the second one
            _usuarios.FallarAlGuardarPorDuplicado = true;

            await Assert.ThrowsAsync<EmailYaRegistradoException>(() => _sut.RegistrarAsync(Request()));
        }

        [Fact]
        public async Task Login_CredencialesValidas_DevuelveTokenYUsuario()
        {
            await _sut.RegistrarAsync(Request());

            var resultado = await _sut.LoginAsync(new LoginDto("ana@example.com", "Secreta123"));

            var guardado = Assert.Single(_usuarios.Guardados);
            Assert.Equal(FakeTokenService.Prefijo + guardado.Id, resultado.AccessToken);
            Assert.Equal(Ahora.AddHours(1), resultado.ExpiraEn);
            Assert.Equal(guardado.Id, resultado.Usuario.Id);
            Assert.Equal("ana@example.com", resultado.Usuario.Email);
        }

        [Fact]
        public async Task Login_EmailConOtraCapitalizacion_Funciona()
        {
            await _sut.RegistrarAsync(Request());

            var resultado = await _sut.LoginAsync(new LoginDto("  ANA@Example.com ", "Secreta123"));

            Assert.Equal("ana@example.com", resultado.Usuario.Email);
        }

        [Fact]
        public async Task Login_PasswordIncorrecta_LanzaCredencialesInvalidas()
        {
            await _sut.RegistrarAsync(Request());

            await Assert.ThrowsAsync<CredencialesInvalidasException>(
                () => _sut.LoginAsync(new LoginDto("ana@example.com", "OtraClave999")));
        }

        [Fact]
        public async Task Login_EmailInexistente_LanzaCredencialesInvalidas()
        {
            await Assert.ThrowsAsync<CredencialesInvalidasException>(
                () => _sut.LoginAsync(new LoginDto("nadie@example.com", "Secreta123")));
        }

        private sealed class FakeUsuarioRepository : IUsuarioRepository
        {
            public List<Usuario> Guardados { get; } = [];
            public bool FallarAlGuardarPorDuplicado { get; set; }

            public Task<bool> ExisteEmailAsync(string email, CancellationToken cancellationToken = default) =>
                Task.FromResult(Guardados.Any(u => u.Email == email));

            public Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken cancellationToken = default) =>
                Task.FromResult(Guardados.FirstOrDefault(u => u.Email == email));

            public Task AddAsync(Usuario usuario, CancellationToken cancellationToken = default)
            {
                if (FallarAlGuardarPorDuplicado)
                    throw new EmailYaRegistradoException();

                Guardados.Add(usuario);
                return Task.CompletedTask;
            }
        }

        private sealed class FakePasswordHasher : IPasswordHasher
        {
            public const string Prefijo = "hash:";

            public string Hash(string password) => Prefijo + password;

            public bool Verify(string? hash, string password) => hash is not null && hash == Prefijo + password;
        }

        private sealed class FakeTokenService : ITokenService
        {
            public const string Prefijo = "token:";

            public (string Token, DateTimeOffset ExpiraEn) Generar(Usuario usuario) =>
                (Prefijo + usuario.Id, Ahora.AddHours(1));
        }

        private sealed class FixedTimeProvider(DateTimeOffset ahora) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => ahora;
        }
    }
}
