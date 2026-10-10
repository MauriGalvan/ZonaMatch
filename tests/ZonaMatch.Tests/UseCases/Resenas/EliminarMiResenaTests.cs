using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Resenas;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Resenas
{
    public class EliminarMiResenaTests
    {
        private const string Zona = FakeZonaRepository.Zona;

        private readonly FakeResenaRepository _resenas = new();
        private readonly EliminarMiResena _casoDeUso;
        private readonly Guid _ana = Guid.NewGuid();
        private readonly Guid _luis = Guid.NewGuid();

        public EliminarMiResenaTests()
        {
            _casoDeUso = new EliminarMiResena(_resenas);
        }

        [Fact]
        public async Task Eliminar_BorraSoloLaResenaDelUsuario()
        {
            _resenas.Agregar(new Resena { ZonaSlug = Zona, UsuarioId = _ana });
            var deLuis = new Resena { ZonaSlug = Zona, UsuarioId = _luis };
            _resenas.Agregar(deLuis);

            await _casoDeUso.EjecutarAsync(Zona, _ana);

            Assert.Same(deLuis, Assert.Single(_resenas.Resenas));
            Assert.Equal(1, _resenas.Guardados);
        }

        [Fact]
        public async Task Eliminar_SinResena_NoEncontrada()
        {
            _resenas.Agregar(new Resena { ZonaSlug = Zona, UsuarioId = _luis });

            await Assert.ThrowsAsync<NoEncontradoException>(() => _casoDeUso.EjecutarAsync(Zona, _ana));
            Assert.Single(_resenas.Resenas);
        }
    }
}
