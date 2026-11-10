using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ZonaMatch.Application.Emails;
using ZonaMatch.Application.UseCases.Review;
using ZonaMatch.Tests.UseCases.Community;
using DomainReview = ZonaMatch.Domain.Entities.Review;

namespace ZonaMatch.Tests.UseCases.Review;

public class ReviewModerationEmailsTests
{
    private const string Author = "ana@example.com";

    private readonly FakeAdministratorRepository _administrators = new();
    private readonly CapturingEmailService _emails = new();
    private readonly ReviewModerationEmails _useCase;

    public ReviewModerationEmailsTests()
    {
        _useCase = new ReviewModerationEmails(
            _administrators,
            _emails,
            new FrontendLinks("http://localhost:5173/"),
            NullLogger<ReviewModerationEmails>.Instance);
        _administrators.AdministratorEmails.AddRange(["admin1@example.com", "admin2@example.com"]);
    }

    // With a safety score of 3 the overall score is (3 + 5 + 4 + 4 + 2) / 5 = 3.6.
    private static DomainReview CreateReview(string text = "Barrio tranquilo y bien conectado con el centro.", int images = 0) => new()
    {
        ZoneSlug = FakeZoneRepository.ZoneSlug,
        SafetyScore = 3,
        TransitScore = 5,
        ConnectivityScore = 4,
        CommerceScore = 4,
        GreenSpaceScore = 2,
        Text = text,
        ImageNames = Enumerable.Range(0, images).Select(index => new string((char)('a' + index), 32) + ".jpg").ToList()
    };

    [Fact]
    public async Task NotifySubmitted_EmailsTheAuthorAndThenEveryAdministrator()
    {
        await _useCase.NotifySubmittedAsync(CreateReview(), Author, isEdit: false);

        _emails.Sent.Select(email => email.To).Should().Equal(Author, "admin1@example.com", "admin2@example.com");
    }

    [Fact]
    public async Task NotifySubmitted_TellsTheAuthorThatTheReviewIsPendingAndLinksToTheZone()
    {
        await _useCase.NotifySubmittedAsync(CreateReview(), Author, isEdit: false);

        var email = _emails.Sent[0];
        email.Subject.Should().Be("Tu reseña en ZonaMatch está pendiente de aprobación");
        email.Body.Should().Contain("Recibimos tu reseña")
            .And.Contain("<strong>villa-luro</strong>")
            .And.Contain("solo vos podés ver tu reseña.")
            .And.Contain("http://localhost:5173/zonas/villa-luro?tab=comunidad");
    }

    [Fact]
    public async Task NotifySubmitted_WhenTheReviewHasImages_MentionsThemToTheAuthor()
    {
        await _useCase.NotifySubmittedAsync(CreateReview(images: 2), Author, isEdit: false);

        _emails.Sent[0].Body.Should().Contain("tu reseña y sus imágenes");
    }

    [Fact]
    public async Task NotifySubmitted_WhenTheAuthorEditedTheReview_SaysSoToBothAudiences()
    {
        await _useCase.NotifySubmittedAsync(CreateReview(), Author, isEdit: true);

        _emails.Sent[0].Body.Should().Contain("Recibimos los cambios de tu reseña");
        _emails.Sent[1].Subject.Should().Be("Una reseña editada espera tu revisión en ZonaMatch");
        _emails.Sent[1].Body.Should().Contain("necesita una nueva aprobación");
    }

    [Fact]
    public async Task NotifySubmitted_GivesTheAdministratorTheDataNeededToDecide()
    {
        await _useCase.NotifySubmittedAsync(CreateReview(images: 2), Author, isEdit: false);

        var email = _emails.Sent[1];
        email.Subject.Should().Be("Hay una reseña para revisar en ZonaMatch");
        email.Body.Should().Contain("<strong>Zona:</strong> villa-luro")
            .And.Contain("<strong>Puntaje general:</strong> 3,6 de 5")
            .And.Contain("<strong>Imágenes:</strong> 2")
            .And.Contain("Barrio tranquilo y bien conectado con el centro.");
    }

    [Fact]
    public async Task NotifySubmitted_EncodesTheTextTheAuthorTyped()
    {
        await _useCase.NotifySubmittedAsync(CreateReview(text: "<script>alert('hola')</script>"), Author, isEdit: false);

        _emails.Sent[1].Body.Should().Contain("&lt;script&gt;alert(&#39;hola&#39;)&lt;/script&gt;")
            .And.NotContain("<script>");
    }

    [Fact]
    public async Task NotifySubmitted_WhenThereAreNoAdministrators_OnlyEmailsTheAuthor()
    {
        _administrators.AdministratorEmails.Clear();

        await _useCase.NotifySubmittedAsync(CreateReview(), Author, isEdit: false);

        _emails.Sent.Select(email => email.To).Should().Equal(Author);
    }

    [Fact]
    public async Task NotifySubmitted_WhenTheAuthorHasNoEmail_OnlyEmailsTheAdministrators()
    {
        await _useCase.NotifySubmittedAsync(CreateReview(), "  ", isEdit: false);

        _emails.Sent.Select(email => email.To).Should().Equal("admin1@example.com", "admin2@example.com");
    }

    [Fact]
    public async Task NotifySubmitted_WhenOneEmailFails_StillSendsTheOthers()
    {
        _emails.FailingRecipients.AddRange([Author, "admin1@example.com"]);

        var act = () => _useCase.NotifySubmittedAsync(CreateReview(), Author, isEdit: false);

        await act.Should().NotThrowAsync();
        _emails.Sent.Select(email => email.To).Should().Equal("admin2@example.com");
    }

    [Fact]
    public async Task NotifySubmitted_WhenTheAdministratorLookupFails_StillEmailsTheAuthor()
    {
        _administrators.FailLookup = true;

        var act = () => _useCase.NotifySubmittedAsync(CreateReview(), Author, isEdit: false);

        await act.Should().NotThrowAsync();
        _emails.Sent.Select(email => email.To).Should().Equal(Author);
    }

    [Fact]
    public async Task NotifySubmitted_WhenTheRequestIsCancelled_PropagatesTheCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var act = () => _useCase.NotifySubmittedAsync(CreateReview(), Author, isEdit: false, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _emails.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task NotifyResolved_WhenApproved_TellsTheAuthorTheReviewIsPublic()
    {
        await _useCase.NotifyResolvedAsync(CreateReview(), Author, approved: true);

        var email = _emails.Sent.Should().ContainSingle().Subject;
        email.To.Should().Be(Author);
        email.Subject.Should().Be("Tu reseña en ZonaMatch fue aprobada");
        email.Body.Should().Contain("Tu reseña ya es pública")
            .And.Contain("<strong>villa-luro</strong>")
            .And.Contain("la comunidad puede ver tu reseña.")
            .And.Contain("http://localhost:5173/zonas/villa-luro?tab=comunidad");
    }

    [Fact]
    public async Task NotifyResolved_WhenApprovedWithImages_MentionsThem()
    {
        await _useCase.NotifyResolvedAsync(CreateReview(images: 1), Author, approved: true);

        _emails.Sent.Single().Body.Should().Contain("tu reseña y sus imágenes");
    }

    [Fact]
    public async Task NotifyResolved_WhenRejected_ExplainsTheReasonAndHowToFixIt()
    {
        var review = CreateReview();
        review.RejectionReason = "Incluye datos <personales>.";

        await _useCase.NotifyResolvedAsync(review, Author, approved: false);

        var email = _emails.Sent.Should().ContainSingle().Subject;
        email.Subject.Should().Be("Tu reseña en ZonaMatch fue rechazada");
        email.Body.Should().Contain("No pudimos publicar tu reseña")
            .And.Contain("<strong>Motivo:</strong> Incluye datos &lt;personales&gt;.")
            .And.Contain("Editar mi reseña")
            .And.Contain("va a volver a revisión");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task NotifyResolved_WhenTheAuthorHasNoEmail_SendsNothing(string? authorEmail)
    {
        await _useCase.NotifyResolvedAsync(CreateReview(), authorEmail, approved: true);

        _emails.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task NotifyResolved_WhenTheEmailFails_DoesNotThrow()
    {
        _emails.FailingRecipients.Add(Author);

        var act = () => _useCase.NotifyResolvedAsync(CreateReview(), Author, approved: true);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task NotifyResolved_DoesNotEmailTheAdministrators()
    {
        await _useCase.NotifyResolvedAsync(CreateReview(), Author, approved: true);

        _emails.Sent.Select(email => email.To).Should().Equal(Author);
    }
}
