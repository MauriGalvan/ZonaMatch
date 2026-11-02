using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Reviews;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Reviews
{
    public class DeleteMyReviewTests
    {
        private const string Zone = FakeZoneRepository.ZoneSlug;

        private readonly FakeReviewRepository _reviews = new();
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
            _reviews.Add(new Review { ZoneSlug = Zone, UserId = _ana });
            var luisReview = new Review { ZoneSlug = Zone, UserId = _luis };
            _reviews.Add(luisReview);

            await _useCase.ExecuteAsync(Zone, _ana);

            Assert.Same(luisReview, Assert.Single(_reviews.Reviews));
            Assert.Equal(1, _reviews.SaveCount);
        }

        [Fact]
        public async Task Delete_WhenReviewDoesNotExist_ThrowsNotFound()
        {
            _reviews.Add(new Review { ZoneSlug = Zone, UserId = _luis });

            await Assert.ThrowsAsync<NotFoundException>(() => _useCase.ExecuteAsync(Zone, _ana));
            Assert.Single(_reviews.Reviews);
        }
    }
}
