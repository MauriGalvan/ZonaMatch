using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Reviews;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Reviews
{
    public class SetReviewHelpfulTests
    {
        private const string Zone = FakeZonaRepository.Zona;
        private static readonly DateTimeOffset CurrentTime = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeResenaRepository _reviews = new();
        private readonly SetReviewHelpful _useCase;
        private readonly Guid _ana = Guid.NewGuid();
        private readonly Guid _luis = Guid.NewGuid();
        private readonly Review _anaReview;

        public SetReviewHelpfulTests()
        {
            _useCase = new SetReviewHelpful(_reviews, new FixedTimeProvider(CurrentTime));
            _anaReview = new Review { ZonaSlug = Zone, UsuarioId = _ana, FechaCreacion = CurrentTime };
            _reviews.Add(_anaReview);
        }

        [Fact]
        public async Task MarkHelpful_Twice_SavesOnlyOneVote()
        {
            await _useCase.ExecuteAsync(Zone, _anaReview.Id, _luis, helpful: true);
            await _useCase.ExecuteAsync(Zone, _anaReview.Id, _luis, helpful: true);

            var vote = Assert.Single(_anaReview.VotosUtil);
            Assert.Equal(_luis, vote.UsuarioId);
            Assert.Equal(CurrentTime, vote.Fecha);
            Assert.Equal(1, _reviews.Guardados);
        }

        [Fact]
        public async Task UnmarkHelpful_RemovesVoteAndIsIdempotent()
        {
            _anaReview.VotosUtil.Add(new ReviewHelpfulVote { ResenaId = _anaReview.Id, UsuarioId = _luis, Fecha = CurrentTime });

            await _useCase.ExecuteAsync(Zone, _anaReview.Id, _luis, helpful: false);
            await _useCase.ExecuteAsync(Zone, _anaReview.Id, _luis, helpful: false);

            Assert.Empty(_anaReview.VotosUtil);
            Assert.Equal(1, _reviews.Guardados);
        }

        [Fact]
        public async Task MarkHelpful_OnOwnReview_IsRejected()
        {
            await Assert.ThrowsAsync<ConflictException>(
                () => _useCase.ExecuteAsync(Zone, _anaReview.Id, _ana, helpful: true));
            Assert.Empty(_anaReview.VotosUtil);
        }

        [Fact]
        public async Task MarkHelpful_WhenReviewDoesNotExist_ThrowsNotFound()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _useCase.ExecuteAsync(Zone, Guid.NewGuid(), _luis, helpful: true));
        }
    }
}
