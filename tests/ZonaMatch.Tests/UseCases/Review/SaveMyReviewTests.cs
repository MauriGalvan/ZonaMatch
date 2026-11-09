using Microsoft.Extensions.Logging.Abstractions;
using ZonaMatch.Application.DTOs.Review.Requests;
using ZonaMatch.Application.Emails;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Review;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;
using ZonaMatch.Tests.UseCases.Image;
using ZonaMatch.Tests.UseCases.Community;

namespace ZonaMatch.Tests.UseCases.Review
{
    public class SaveMyReviewTests
    {
        private const string Zone = FakeZoneRepository.ZoneSlug;
        private static readonly DateTimeOffset CurrentTime = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeReviewRepository _reviews = new();
        private readonly FakeImageStorage _images = new();
        private readonly FakeAdministratorRepository _administrators = new();
        private readonly FakeNotificationRepository _notifications = new();
        private readonly CapturingEmailService _emails = new();
        private readonly SaveMyReview _useCase;
        private readonly AuthenticatedUserContext _ana = new(Guid.NewGuid(), "ana@example.com");

        public SaveMyReviewTests()
        {
            _useCase = new SaveMyReview(
                _reviews,
                new FakeZoneRepository(),
                _images,
                _administrators,
                _notifications,
                new ReviewModerationEmails(
                    _administrators,
                    _emails,
                    new FrontendLinks("http://localhost:5173/"),
                    NullLogger<ReviewModerationEmails>.Instance),
                new FixedTimeProvider(CurrentTime));
            _administrators.AdministratorEmails.AddRange(["admin1@example.com", "admin2@example.com"]);
        }

        private static SaveReviewRequest Review(
            int safety = 3,
            string[]? topics = null,
            string[]? images = null,
            string text = "Barrio tranquilo y bien conectado con el centro.") =>
            new(new ReviewAspectScoresRequest(safety, 5, 4, 4, 2), $"  {text}  ", 9, topics, images);

        // The administrator approves the user's review, as ResolveReview does.
        private void Approve() =>
            Assert.Single(_reviews.Reviews).Resolve(Guid.NewGuid(), approve: true, rejectionReason: null, CurrentTime);

        private static string ImageName(char digit) => new string(digit, 32) + ".png";

        [Fact]
        public async Task Save_WithImages_StoresTheirNamesWithoutDuplicatesAndReturnsTheirUrls()
        {
            var response = await _useCase.ExecuteAsync(Zone, _ana, Review(images: [ImageName('1'), ImageName('1'), ImageName('2')]));

            var savedReview = Assert.Single(_reviews.Reviews);
            Assert.Equal([ImageName('1'), ImageName('2')], savedReview.ImageNames);
            Assert.Equal([ImageName('1'), ImageName('2')], response.Images.Select(image => image.Name));
            Assert.Equal($"https://images.test/{ImageName('1')}", response.Images[0].Url);
        }

        [Fact]
        public async Task Save_WithoutImages_RemovesThePreviousOnes()
        {
            await _useCase.ExecuteAsync(Zone, _ana, Review(images: [ImageName('1')]));
            var second = await _useCase.ExecuteAsync(Zone, _ana, Review());

            Assert.Empty(Assert.Single(_reviews.Reviews).ImageNames);
            Assert.Empty(second.Images);
        }

        [Fact]
        public async Task Save_WithTooManyImages_ThrowsBadRequest()
        {
            string[] images = [ImageName('1'), ImageName('2'), ImageName('3'), ImageName('4')];

            await Assert.ThrowsAsync<BadRequestException>(() => _useCase.ExecuteAsync(Zone, _ana, Review(images: images)));

            Assert.Empty(_reviews.Reviews);
        }

        [Theory]
        [InlineData("../secret.png")]
        [InlineData("photo.jpg")]
        [InlineData("11111111111111111111111111111111.gif")]
        public async Task Save_WithAnInvalidImageName_ThrowsBadRequest(string name)
        {
            await Assert.ThrowsAsync<BadRequestException>(() => _useCase.ExecuteAsync(Zone, _ana, Review(images: [name])));

            Assert.Empty(_reviews.Reviews);
        }

        [Fact]
        public async Task Save_Twice_ReplacesTheUsersReview()
        {
            await _useCase.ExecuteAsync(Zone, _ana, Review(safety: 3));
            var second = await _useCase.ExecuteAsync(Zone, _ana, Review(safety: 1, topics: ["Transporte", "seguridad"]));

            var savedReview = Assert.Single(_reviews.Reviews);
            Assert.Equal(1, savedReview.SafetyScore);
            Assert.Equal(5, savedReview.TransitScore);
            Assert.Equal("Barrio tranquilo y bien conectado con el centro.", savedReview.Text);
            Assert.Equal([ReviewAspect.Safety, ReviewAspect.PublicTransport], savedReview.Topics);
            Assert.Equal(CurrentTime, savedReview.CreatedAt);
            Assert.True(second.IsMine);
            Assert.Equal("AN", second.Initials);
        }

        [Fact]
        public async Task Save_WhenCreatingReview_LeavesItPendingAndReturnsItsStatusToTheAuthor()
        {
            var response = await _useCase.ExecuteAsync(Zone, _ana, Review());

            var savedReview = Assert.Single(_reviews.Reviews);
            Assert.Equal(ReviewStatus.Pending, savedReview.Status);
            Assert.Null(savedReview.PublishedAt);
            Assert.Equal(ReviewStatus.Pending, response.Status);
            Assert.True(response.IsMine);
        }

        [Fact]
        public async Task Save_WhenEditingAnApprovedReviewWithoutChangingItsContent_KeepsItApproved()
        {
            await _useCase.ExecuteAsync(Zone, _ana, Review(images: [ImageName('1')]));
            Approve();

            var response = await _useCase.ExecuteAsync(Zone, _ana, Review(safety: 1, topics: ["transporte"], images: [ImageName('1')]));

            var savedReview = Assert.Single(_reviews.Reviews);
            Assert.Equal(1, savedReview.SafetyScore);
            Assert.Equal(ReviewStatus.Approved, savedReview.Status);
            Assert.Equal(ReviewStatus.Approved, response.Status);
        }

        [Fact]
        public async Task Save_WhenTheTextOfAnApprovedReviewChanges_SendsItBackToPendingButKeepsItsPublicationDate()
        {
            await _useCase.ExecuteAsync(Zone, _ana, Review());
            Approve();

            await _useCase.ExecuteAsync(Zone, _ana, Review(text: "Cambié de opinión sobre el barrio después de un año."));

            var savedReview = Assert.Single(_reviews.Reviews);
            Assert.Equal(ReviewStatus.Pending, savedReview.Status);
            Assert.Null(savedReview.ResolvedAt);
            Assert.Null(savedReview.ResolvedByUserId);
            Assert.Equal(CurrentTime, savedReview.PublishedAt);
            Assert.Equal("Cambié de opinión sobre el barrio después de un año.", savedReview.Text);
        }

        [Fact]
        public async Task Save_WhenTheImagesOfAnApprovedReviewChange_SendsItBackToPending()
        {
            await _useCase.ExecuteAsync(Zone, _ana, Review(images: [ImageName('1')]));
            Approve();

            await _useCase.ExecuteAsync(Zone, _ana, Review(images: [ImageName('1'), ImageName('2')]));

            Assert.Equal(ReviewStatus.Pending, Assert.Single(_reviews.Reviews).Status);
        }

        [Fact]
        public async Task Save_WhenEditingARejectedReview_SendsItBackToPendingAndClearsTheReason()
        {
            await _useCase.ExecuteAsync(Zone, _ana, Review());
            Assert.Single(_reviews.Reviews).Resolve(Guid.NewGuid(), approve: false, "El texto incluye datos personales.", CurrentTime);

            var response = await _useCase.ExecuteAsync(Zone, _ana, Review(safety: 2));

            var savedReview = Assert.Single(_reviews.Reviews);
            Assert.Equal(ReviewStatus.Pending, savedReview.Status);
            Assert.Null(savedReview.RejectionReason);
            Assert.Null(response.RejectionReason);
        }

        [Fact]
        public async Task Save_WhenCreatingReview_EmailsTheAuthorAndEveryAdministrator()
        {
            await _useCase.ExecuteAsync(Zone, _ana, Review());

            Assert.Equal(["ana@example.com", "admin1@example.com", "admin2@example.com"], _emails.Sent.Select(email => email.To));
            Assert.Equal("Tu reseña en ZonaMatch está pendiente de aprobación", _emails.Sent[0].Subject);
            Assert.Contains("Recibimos tu reseña", _emails.Sent[0].Body);
            Assert.Equal("Hay una reseña para revisar en ZonaMatch", _emails.Sent[1].Subject);
            Assert.Contains("Barrio tranquilo y bien conectado con el centro.", _emails.Sent[1].Body);
        }

        [Fact]
        public async Task Save_WhenAnApprovedReviewChanges_EmailsAgainAsAnEdit()
        {
            await _useCase.ExecuteAsync(Zone, _ana, Review());
            Approve();
            _emails.Sent.Clear();

            await _useCase.ExecuteAsync(Zone, _ana, Review(text: "Cambié de opinión sobre el barrio después de un año."));

            Assert.Equal(["ana@example.com", "admin1@example.com", "admin2@example.com"], _emails.Sent.Select(email => email.To));
            Assert.Contains("Recibimos los cambios de tu reseña", _emails.Sent[0].Body);
            Assert.Equal("Una reseña editada espera tu revisión en ZonaMatch", _emails.Sent[1].Subject);
        }

        [Fact]
        public async Task Save_WhenARejectedReviewIsCorrected_EmailsAgain()
        {
            await _useCase.ExecuteAsync(Zone, _ana, Review());
            Assert.Single(_reviews.Reviews).Resolve(Guid.NewGuid(), approve: false, "Incluye datos personales.", CurrentTime);
            _emails.Sent.Clear();

            await _useCase.ExecuteAsync(Zone, _ana, Review(safety: 2));

            Assert.Equal(3, _emails.Sent.Count);
        }

        [Fact]
        public async Task Save_WhenSubmittedForApproval_NotifiesEveryAdministratorInTheApp()
        {
            var admin1 = Guid.NewGuid();
            var admin2 = Guid.NewGuid();
            _administrators.AdministratorIds.AddRange([admin1, admin2, _ana.Id]);

            await _useCase.ExecuteAsync(Zone, _ana, Review());

            Assert.Equal(new[] { admin1, admin2, _ana.Id }, _notifications.Notifications.Select(n => n.UserId).ToArray());
            Assert.All(_notifications.Notifications, n =>
            {
                Assert.Equal(NotificationType.ReviewPendingModeration, n.Type);
                Assert.Contains(Zone, n.Link);
                Assert.NotNull(n.ReviewId);
                Assert.Null(n.ReadAt);
            });
        }

        [Fact]
        public async Task Save_WhenEditingAReviewThatIsStillPending_DoesNotNotifyTheAdministratorsAgain()
        {
            _administrators.AdministratorIds.Add(Guid.NewGuid());
            await _useCase.ExecuteAsync(Zone, _ana, Review());
            _notifications.Notifications.Clear();

            await _useCase.ExecuteAsync(Zone, _ana, Review(text: "Cambié el texto antes de que lo revisen."));

            Assert.Empty(_notifications.Notifications);
        }

        [Fact]
        public async Task Save_WhenEditingAReviewThatIsStillPending_DoesNotEmailAgain()
        {
            await _useCase.ExecuteAsync(Zone, _ana, Review());
            _emails.Sent.Clear();

            await _useCase.ExecuteAsync(Zone, _ana, Review(text: "Cambié el texto antes de que lo revisen."));

            Assert.Empty(_emails.Sent);
        }

        [Fact]
        public async Task Save_WhenAnApprovedReviewKeepsItsContent_DoesNotEmail()
        {
            await _useCase.ExecuteAsync(Zone, _ana, Review());
            Approve();
            _emails.Sent.Clear();

            await _useCase.ExecuteAsync(Zone, _ana, Review(safety: 1));

            Assert.Empty(_emails.Sent);
        }

        [Fact]
        public async Task Save_WhenTheEmailsFail_StillSavesTheReview()
        {
            _emails.FailingRecipients.AddRange(["ana@example.com", "admin1@example.com", "admin2@example.com"]);

            var response = await _useCase.ExecuteAsync(Zone, _ana, Review());

            Assert.Single(_reviews.Reviews);
            Assert.Equal(1, _reviews.SaveCount);
            Assert.Equal(ReviewStatus.Pending, response.Status);
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
            Assert.Empty(_reviews.Reviews);
        }

        [Fact]
        public async Task Save_WhenZoneDoesNotExist_ThrowsNotFound()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _useCase.ExecuteAsync("no-existe", _ana, Review()));
            Assert.Empty(_reviews.Reviews);
        }
    }
}
