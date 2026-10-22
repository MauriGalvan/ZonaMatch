using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Contacto;

namespace ZonaMatch.Tests.UseCases.Contacto
{
    public class EnviarFormularioContactoTests
    {
        private const string Equipo = "equipo@example.com";
        private static readonly DateTimeOffset Ahora = new(2026, 10, 20, 18, 30, 0, TimeSpan.Zero);

        private readonly FakeEmailService _email = new();
        private readonly EnviarFormularioContacto _casoDeUso;

        public EnviarFormularioContactoTests()
        {
            _casoDeUso = new EnviarFormularioContacto(_email, new ContactoOpciones(Equipo), new FixedTimeProvider(Ahora));
        }

        [Fact]
        public async Task Enviar_MandaUnMailAlEquipoConLosDatos()
        {
            await _casoDeUso.EjecutarAsync(new FormularioContactoDto("  ana@example.com ", "  Ana  ", "Caballito\r\no Almagro", null));

            var mail = Assert.Single(_email.Enviados);
            Assert.Equal(Equipo, mail.Para);
            Assert.Equal("Formulario de contacto: Ana", mail.Asunto);
            Assert.Contains("ana@example.com", mail.Cuerpo);
            Assert.Contains("Caballito o Almagro", mail.Cuerpo);
            // 18:30 UTC son las 15:30 en Argentina
            Assert.Contains("20/10/2026 15:30", mail.Cuerpo);
        }

        [Fact]
        public async Task Enviar_SinNombreNiZona_LoAclaraEnElMail()
        {
            await _casoDeUso.EjecutarAsync(new FormularioContactoDto("ana@example.com", "  ", null, null));

            var mail = Assert.Single(_email.Enviados);
            Assert.Equal("Formulario de contacto: ana@example.com", mail.Asunto);
            Assert.Contains("<strong>Nombre:</strong> No lo indico", mail.Cuerpo);
            Assert.Contains("<strong>Donde busca:</strong> No lo indico", mail.Cuerpo);
        }

        [Fact]
        public async Task Enviar_CodificaElHtmlQueEscribeLaPersona()
        {
            await _casoDeUso.EjecutarAsync(new FormularioContactoDto("ana@example.com", "<b>Ana</b>", "<script>alert(1)</script>", null));

            var mail = Assert.Single(_email.Enviados);
            Assert.DoesNotContain("<script>", mail.Cuerpo);
            Assert.Contains("&lt;script&gt;", mail.Cuerpo);
            Assert.Contains("&lt;b&gt;Ana&lt;/b&gt;", mail.Cuerpo);
        }

        [Fact]
        public async Task Enviar_ConElCampoTrampaCompleto_NoMandaNada()
        {
            await _casoDeUso.EjecutarAsync(new FormularioContactoDto("bot@example.com", null, null, "https://spam.example.com"));

            Assert.Empty(_email.Enviados);
        }

        [Fact]
        public async Task Enviar_SiFallaElCorreo_ServicioNoDisponible()
        {
            _email.Falla = new InvalidOperationException("SMTP caido");

            var error = await Assert.ThrowsAsync<ServiceUnavailableException>(
                () => _casoDeUso.EjecutarAsync(new FormularioContactoDto("ana@example.com", null, null, null)));
            Assert.IsType<InvalidOperationException>(error.InnerException);
        }

        private sealed class FakeEmailService : IEmailService
        {
            public List<(string Para, string Asunto, string Cuerpo)> Enviados { get; } = [];
            public Exception? Falla { get; set; }

            public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
            {
                if (Falla is not null)
                    throw Falla;

                Enviados.Add((to, subject, body));
                return Task.CompletedTask;
            }
        }
    }
}
