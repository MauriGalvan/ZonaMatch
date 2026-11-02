using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Reviews;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Reviews
{
    public class DeleteMyReviewTests
    {
        private const string Zone = FakeZonaRepository.Zona;

        private readonly FakeResenaRepository _reviews = new();
        private readonly DeleteMyReview _useCase;
        private readonly Guid _ana = Guid.NewGuid();
        private readonly Guid _luis = Guid.NewGuid();

        public DeleteMyReviewTests()
        {
            _useCase = new DeleteMyReview(_reviews);
        }

        [Fact]
        public async Task Delete_RemovesOnlyTheCurrentUsersReview()
        {
            _reviews.Agregar(new Review { ZonaSlug = Zone, UsuarioId = _ana });
            var luisReview = new Review { ZonaSlug = Zone, UsuarioId = _luis };
            _reviews.Agregar(luisReview);

            await _useCase.ExecuteAsync(Zone, _ana);

            Assert.Same(luisReview, Assert.Single(_reviews.Resenas));
            Assert.Equal(1, _reviews.Guardados);
        }

        [Fact]
        public async Task Delete_WhenReviewDoesNotExist_ThrowsNotFound()
        {
            _reviews.Agregar(new Review { ZonaSlug = Zone, UsuarioId = _luis });

            await Assert.ThrowsAsync<NotFoundException>(() => _useCase.ExecuteAsync(Zone, _ana));
            Assert.Single(_reviews.Resenas);
        }
    }
}
