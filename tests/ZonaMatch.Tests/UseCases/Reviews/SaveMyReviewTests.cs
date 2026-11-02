using ZonaMatch.Application.DTOs.Reviews.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Reviews;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Tests.UseCases.Reviews
{
    public class SaveMyReviewTests
    {
        private const string Zone = FakeZonaRepository.Zona;
        private static readonly DateTimeOffset CurrentTime = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeResenaRepository _reviews = new();
        private readonly SaveMyReview _useCase;
        private readonly AuthenticatedUserContext _ana = new(Guid.NewGuid(), "ana@example.com");

        public SaveMyReviewTests()
        {
            _useCase = new SaveMyReview(
                _reviews,
                new FakeZonaRepository(),
                new FakeSavedZoneRepository(),
                new FakeNotificationRepository(),
                new FixedTimeProvider(CurrentTime));
        }

        private static SaveReviewRequest Review(int safety = 3, string[]? topics = null) =>
            new(new ReviewAspectScoresRequest(safety, 5, 4, 4, 2), "  Barrio tranquilo y bien conectado con el centro.  ", 9, topics);

        [Fact]
        public async Task Save_Twice_ReplacesTheUsersReview()
        {
            await _useCase.ExecuteAsync(Zone, _ana, Review(safety: 3));
            var second = await _useCase.ExecuteAsync(Zone, _ana, Review(safety: 1, topics: ["Transporte", "seguridad"]));

            var savedReview = Assert.Single(_reviews.Resenas);
            Assert.Equal(1, savedReview.PuntajeSeguridad);
            Assert.Equal(5, savedReview.PuntajeTransporte);
            Assert.Equal("Barrio tranquilo y bien conectado con el centro.", savedReview.Texto);
            Assert.Equal([ReviewAspect.Safety, ReviewAspect.PublicTransport], savedReview.Temas);
            Assert.Equal(CurrentTime, savedReview.FechaCreacion);
            Assert.True(second.IsMine);
            Assert.Equal("AN", second.Initials);
        }

        [Fact]
        public async Task Save_ReturnsTheOverallScoreCalculatedFromTheAspects()
        {
            var savedReview = await _useCase.ExecuteAsync(Zone, _ana, Review(safety: 3));

            Assert.Equal(3.6, savedReview.Rating);
        }

        [Fact]
        public async Task Save_WithUnknownTopic_ThrowsBadRequest()
        {
            await Assert.ThrowsAsync<BadRequestException>(() => _useCase.ExecuteAsync(Zone, _ana, Review(topics: ["ruido"])));
            Assert.Empty(_reviews.Resenas);
        }

        [Fact]
        public async Task Save_WhenZoneDoesNotExist_ThrowsNotFound()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _useCase.ExecuteAsync("no-existe", _ana, Review()));
            Assert.Empty(_reviews.Resenas);
        }
    }
}
