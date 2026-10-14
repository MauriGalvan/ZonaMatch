using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Perfil;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Perfil
{
    public class GuardarMiPerfilTests
    {
        private static readonly DateTimeOffset Ahora = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
        private static readonly Guid Usuario = Guid.NewGuid();

        private readonly FakePerfilRepository _perfiles = new();
        private readonly GuardarMiPerfil _sut;

        public GuardarMiPerfilTests()
        {
            _sut = new GuardarMiPerfil(_perfiles, new FakeCriterioRepository(), new FixedTimeProvider(Ahora));
        }

        [Fact]
        public async Task SinPerfil_LoCreaComoPerfilConSusFilas()
        {
            var dto = await _sut.EjecutarAsync(Usuario, Pedidos.Criterio(
                new() { ["seguridad"] = "alta", ["salud"] = "baja" },
                personas: 2,
                edades: ["26–35 años"],
                lugares: [new LugarRequest { Nombre = " Facultad ", Latitude = -34.6, Longitude = -58.4, Tipo = "Estudio" }],
                alquiler: "800000"));

            var perfil = Assert.Single(_perfiles.Perfiles);
            Assert.Equal(TipoContexto.Perfil, perfil.Tipo);
            Assert.Equal(Usuario, perfil.UsuarioId);
            Assert.Equal(Ahora, perfil.FechaCreacion);
            Assert.Equal(2, perfil.Integrantes.Count);
            Assert.Equal(["26–35 años", null], perfil.Integrantes.OrderBy(p => p.Orden).Select(p => p.Edad));
            Assert.Equal("Facultad", Assert.Single(perfil.Puntos).Nombre);
            Assert.Equal("800000", perfil.AlquilerMaximo);

            Assert.Equal("alta", dto.Criterios["seguridad"]);
            Assert.Equal("baja", dto.Criterios["salud"]);
            Assert.Equal(1, _perfiles.Guardados);
        }

        [Fact]
        public async Task ConPerfil_LoReemplazaSinCrearOtro()
        {
            await _sut.EjecutarAsync(Usuario, Pedidos.Criterio(new() { ["seguridad"] = "alta" }, personas: 3));

            await _sut.EjecutarAsync(Usuario, Pedidos.Criterio(new() { ["salud"] = "media" }, personas: 1));

            var perfil = Assert.Single(_perfiles.Perfiles);
            Assert.Single(perfil.Integrantes);
            var criterio = Assert.Single(perfil.Criterios);
            Assert.Equal("salud", criterio.CriterioId);
        }

        [Theory]
        [InlineData("turismo", "alta", "Criterio desconocido")]
        [InlineData("transporte", "alta", "Criterio no disponible")]
        [InlineData("seguridad", "urgentisima", "Prioridad inválida")]
        [InlineData("seguridad", null, "Prioridad inválida")]
        public async Task CriterioInvalido_LanzaErrorYNoGuarda(string criterio, string? prioridad, string mensaje)
        {
            var error = await Assert.ThrowsAsync<DatosInvalidosException>(
                () => _sut.EjecutarAsync(Usuario, Pedidos.Criterio(new() { [criterio] = prioridad })));

            Assert.Contains(mensaje, error.Message);
            Assert.Empty(_perfiles.Perfiles);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(9)]
        public async Task PersonasFueraDeRango_LanzaError(int personas)
        {
            await Assert.ThrowsAsync<DatosInvalidosException>(
                () => _sut.EjecutarAsync(Usuario, Pedidos.Criterio(personas: personas)));

            Assert.Empty(_perfiles.Perfiles);
        }

        [Fact]
        public async Task LugarSinNombre_LanzaErrorYNoTocaElPerfilExistente()
        {
            await _sut.EjecutarAsync(Usuario, Pedidos.Criterio(new() { ["seguridad"] = "alta" }));

            await Assert.ThrowsAsync<DatosInvalidosException>(() => _sut.EjecutarAsync(Usuario, Pedidos.Criterio(
                new() { ["salud"] = "media" },
                lugares: [new LugarRequest { Nombre = "  " }])));

            // Nada a medio cambiar: el perfil sigue con el criterio anterior
            Assert.Equal("seguridad", Assert.Single(Assert.Single(_perfiles.Perfiles).Criterios).CriterioId);
        }

        [Fact]
        public async Task ObtenerMiPerfil_SinPerfil_DevuelveNull()
        {
            Assert.Null(await new ObtenerMiPerfil(_perfiles).EjecutarAsync(Usuario));
        }
    }
}
