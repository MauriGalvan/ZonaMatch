using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.PuntosInteres;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.PuntosInteres
{
    public class CrearMiPuntoInteresTests
    {
        private static readonly DateTimeOffset Ahora = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

        private readonly FakePuntoInteresRepository _puntos = new();
        private readonly CrearMiPuntoInteres _crear;
        private readonly ListarMisPuntosInteres _listar;
        private readonly Guid _usuario = Guid.NewGuid();

        public CrearMiPuntoInteresTests()
        {
            _crear = new CrearMiPuntoInteres(_puntos, new FakeGeolocationService(), new FixedTimeProvider(Ahora));
            _listar = new ListarMisPuntosInteres(_puntos);
        }

        private static CrearPuntoInteresUsuarioDto Solicitud(string tag = "Trabajo", string? alias = null, double latitud = -34.62) =>
            new(tag, alias, latitud, -58.44);

        [Fact]
        public async Task Crear_GuardaElPuntoConTagYConLaDireccionYElBarrioDelServidor()
        {
            var dto = await _crear.EjecutarAsync(_usuario, Solicitud(tag: "  Trabajo ", alias: "  Oficina "));

            var guardado = Assert.Single(_puntos.Puntos);
            Assert.Equal(_usuario, guardado.UsuarioId);
            Assert.Equal("Trabajo", guardado.Tag.Nombre);
            Assert.Equal("Oficina", guardado.Alias);
            Assert.Equal("Somellera 1499", guardado.Direccion);
            Assert.Equal("Parque Chacabuco", guardado.Barrio);
            Assert.Equal(-34.62, guardado.Ubicacion.Y);
            Assert.Equal(-58.44, guardado.Ubicacion.X);
            Assert.Equal(4326, guardado.Ubicacion.SRID);
            Assert.Equal(Ahora, guardado.FechaCreacion);

            Assert.Equal(guardado.Id, dto.Id);
            Assert.Equal("Trabajo", dto.Tag);
            Assert.Equal(new CoordenadaDto(-34.62, -58.44), dto.Ubicacion);
        }

        [Fact]
        public async Task Crear_AliasEnBlanco_QuedaSinAlias()
        {
            var dto = await _crear.EjecutarAsync(_usuario, Solicitud(alias: "   "));

            Assert.Null(dto.Alias);
        }

        [Fact]
        public async Task Crear_ConUnTagQueYaExiste_ReutilizaElTag()
        {
            await _crear.EjecutarAsync(_usuario, Solicitud(tag: "Peluquería"));
            var segundo = await _crear.EjecutarAsync(_usuario, Solicitud(tag: " peluqueria "));

            Assert.Equal(2, _puntos.Puntos.Count);
            Assert.Single(_puntos.Tags);
            Assert.Same(_puntos.Puntos[0].Tag, _puntos.Puntos[1].Tag);
            Assert.Equal("Peluquería", segundo.Tag);
        }

        [Fact]
        public async Task Crear_FueraDelAmba_EsInvalido()
        {
            await Assert.ThrowsAsync<DatosInvalidosException>(() => _crear.EjecutarAsync(_usuario, Solicitud(latitud: 10)));

            Assert.Empty(_puntos.Puntos);
        }

        [Fact]
        public async Task Crear_TagMuyCortoDespuesDeLimpiarlo_EsInvalido()
        {
            await Assert.ThrowsAsync<DatosInvalidosException>(() => _crear.EjecutarAsync(_usuario, Solicitud(tag: "  a  ")));

            Assert.Empty(_puntos.Puntos);
        }

        [Fact]
        public async Task Listar_DevuelveSoloLosPuntosDelUsuario()
        {
            var otro = Guid.NewGuid();
            await _crear.EjecutarAsync(_usuario, Solicitud(tag: "Trabajo"));
            await _crear.EjecutarAsync(_usuario, Solicitud(tag: "Colegio"));
            await _crear.EjecutarAsync(otro, Solicitud(tag: "Club"));

            var mios = await _listar.EjecutarAsync(_usuario);

            Assert.Equal(2, mios.Count);
            Assert.DoesNotContain(mios, p => p.Tag == "Club");
        }
    }

    // Repositorio en memoria: solo implementa los puntos personales; las consultas del mapa no se usan aca
    internal sealed class FakePuntoInteresRepository : IPuntoInteresRepository
    {
        public List<PuntoInteresUsuario> Puntos { get; } = [];
        public List<TagPuntoInteres> Tags { get; } = [];

        public Task AgregarDeUsuarioAsync(PuntoInteresUsuario punto, string tag, CancellationToken cancellationToken = default)
        {
            var nombre = TagPuntoInteres.LimpiarNombre(tag);
            var normalizado = TagPuntoInteres.Normalizar(nombre);

            var existente = Tags.FirstOrDefault(t => t.NombreNormalizado == normalizado);
            if (existente is null)
            {
                existente = new TagPuntoInteres { Nombre = nombre, NombreNormalizado = normalizado };
                Tags.Add(existente);
            }

            punto.Tag = existente;
            punto.TagId = existente.Id;
            Puntos.Add(punto);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PuntoInteresUsuario>> ListarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PuntoInteresUsuario>>(
                Puntos.Where(p => p.UsuarioId == usuarioId).OrderByDescending(p => p.FechaCreacion).ToList());

        public Task<IReadOnlyList<PuntoInteresDto>> GetCercanosAsync(
            double latitud, double longitud, double radioMetros, IReadOnlyCollection<string> categorias, int limite,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<CantidadPorTipo>> ContarPorTipoAsync(
            double latitud, double longitud, double radioMetros, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PuntoInteresDto>> GetEnZonaAsync(
            string slug, IReadOnlyCollection<string> categorias, int limite, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CantidadPorTipo>> ContarPorTipoEnZonaAsync(string slug, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    // Todo punto con latitud menor a 0 cae en la comuna 7 (Parque Chacabuco); el resto esta fuera del AMBA
    internal sealed class FakeGeolocationService : IGeolocationService
    {
        public Task<DireccionDto?> GetDireccionPorUbicacionAsync(double latitud, double longitud, CancellationToken cancellationToken = default) =>
            Task.FromResult<DireccionDto?>(latitud < 0
                ? new DireccionDto(
                    new CoordenadaDto(latitud, longitud), Nombre: null, Calle: "Somellera", Altura: "1499",
                    Localidad: "Parque Chacabuco", Partido: null, Comuna: "Comuna 7", Provincia: "CABA",
                    CodigoPostal: null, DireccionCompleta: "Somellera 1499, Parque Chacabuco")
                : null);

        public Task<UbicacionResumenDto> GetResumenUbicacionAsync(double latitud, double longitud, double radioMetros) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<EscuelaDto>> GetEscuelasCercanasAsync(
            double latitud, double longitud, double radioMetros, string? nivel = null, string? sector = null) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<EscuelaDto>> GetEscuelasMasCercanasAsync(double latitud, double longitud, int cantidad) =>
            throw new NotSupportedException();

        public Task<PartidoDto?> GetPartidoPorUbicacionAsync(double latitud, double longitud) => throw new NotSupportedException();

        public Task<ComunaDto?> GetComunaPorUbicacionAsync(double latitud, double longitud) => throw new NotSupportedException();

        public Task<PuntosInteresCercanosDto> GetPuntosInteresCercanosAsync(
            double latitud, double longitud, double radioMetros, IReadOnlyCollection<string> categorias,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ResumenPuntosInteresDto> GetResumenPuntosInteresAsync(
            double latitud, double longitud, double radioMetros, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
