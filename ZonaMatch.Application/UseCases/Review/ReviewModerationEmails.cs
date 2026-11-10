using System.Globalization;
using Microsoft.Extensions.Logging;
using ZonaMatch.Application.Emails;
using ZonaMatch.Application.Interfaces;
using DomainReview = ZonaMatch.Domain.Entities.Review;

namespace ZonaMatch.Application.UseCases.Review
{
    // Emails of the review moderation flow. When a review is submitted for approval the author learns that it is pending
    // and every site administrator learns that there is a review to moderate; when an administrator resolves it, the
    // author learns the outcome. The change is already saved by then, so a failed email is logged and never fails the request.
    public sealed class ReviewModerationEmails
    {
        private readonly IUserRepository _users;
        private readonly IEmailService _emailService;
        private readonly FrontendLinks _links;
        private readonly ILogger<ReviewModerationEmails> _logger;

        public ReviewModerationEmails(
            IUserRepository users,
            IEmailService emailService,
            FrontendLinks links,
            ILogger<ReviewModerationEmails> logger)
        {
            _users = users;
            _emailService = emailService;
            _links = links;
            _logger = logger;
        }

        // isEdit: the author changed a review that already existed, instead of publishing a new one.
        public async Task NotifySubmittedAsync(
            DomainReview review, string authorEmail, bool isEdit, CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(authorEmail))
            {
                var authorBody = AuthorBody(review, isEdit);
                await TryAsync(
                    () => _emailService.SendEmailAsync(authorEmail, "Tu reseña en ZonaMatch está pendiente de aprobación", authorBody, cancellationToken),
                    review.Id, "email the author", cancellationToken);
            }

            IReadOnlyList<string> administrators = [];
            await TryAsync(
                async () => administrators = await _users.GetAdministratorEmailsAsync(cancellationToken),
                review.Id, "look up the administrators", cancellationToken);

            if (administrators.Count == 0)
                _logger.LogWarning("No administrators to notify about community review {ReviewId}: flag a user with es_administrador.", review.Id);

            var subject = isEdit ? "Una reseña editada espera tu revisión en ZonaMatch" : "Hay una reseña para revisar en ZonaMatch";
            var administratorBody = AdministratorBody(review, isEdit);
            foreach (var administrator in administrators)
            {
                await TryAsync(
                    () => _emailService.SendEmailAsync(administrator, subject, administratorBody, cancellationToken),
                    review.Id, "email an administrator", cancellationToken);
            }
        }

        // The author learns that an administrator approved the review or rejected it, and why.
        public async Task NotifyResolvedAsync(
            DomainReview review, string? authorEmail, bool approved, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(authorEmail))
                return;

            var recipient = authorEmail.Trim();
            var subject = approved ? "Tu reseña en ZonaMatch fue aprobada" : "Tu reseña en ZonaMatch fue rechazada";
            var body = ResolvedBody(review, approved);

            await TryAsync(
                () => _emailService.SendEmailAsync(recipient, subject, body, cancellationToken),
                review.Id, "email the author the outcome", cancellationToken);
        }

        private string ResolvedBody(DomainReview review, bool approved)
        {
            var zone = BrandEmail.Encode(review.ZoneSlug);
            var link = _links.To($"/zonas/{Uri.EscapeDataString(review.ZoneSlug)}?tab=comunidad");

            if (approved)
            {
                return BrandEmail.Render(new BrandEmailContent(
                    Preheader: "Ya la puede ver toda la comunidad.",
                    Title: "Tu reseña ya es pública",
                    Paragraphs:
                    [
                        $"Un administrador aprobó tu reseña de la zona <strong>{zone}</strong>.",
                        review.ImageNames.Count > 0
                            ? "Ahora la comunidad puede ver tu reseña y sus imágenes."
                            : "Ahora la comunidad puede ver tu reseña.",
                    ],
                    Button: new BrandEmailButton("Ver mi reseña", link),
                    Footnote: "Te escribimos porque publicaste esta reseña."));
            }

            return BrandEmail.Render(new BrandEmailContent(
                Preheader: "Un administrador revisó tu reseña y no la aprobó.",
                Title: "No pudimos publicar tu reseña",
                Paragraphs:
                [
                    $"Un administrador revisó tu reseña de la zona <strong>{zone}</strong> y no la aprobó.",
                    $"<strong>Motivo:</strong> {BrandEmail.Encode(review.RejectionReason ?? "No se indicó un motivo.")}",
                    "Podés editarla y enviarla de nuevo: va a volver a revisión. Mientras tanto, solo vos podés verla.",
                ],
                Button: new BrandEmailButton("Editar mi reseña", link),
                Footnote: "Te escribimos porque publicaste esta reseña."));
        }

        private string AuthorBody(DomainReview review, bool isEdit)
        {
            var zone = BrandEmail.Encode(review.ZoneSlug);

            return BrandEmail.Render(new BrandEmailContent(
                Preheader: "Un administrador la revisa antes de publicarla.",
                Title: isEdit ? "Recibimos los cambios de tu reseña" : "Recibimos tu reseña",
                Paragraphs:
                [
                    isEdit
                        ? $"Actualizaste tu reseña de la zona <strong>{zone}</strong>, así que un administrador tiene que revisarla de nuevo antes de que vuelva a ser pública."
                        : $"Gracias por reseñar la zona <strong>{zone}</strong>. Un administrador la va a revisar antes de publicarla.",
                    review.ImageNames.Count > 0
                        ? "Mientras tanto, solo vos podés ver tu reseña y sus imágenes."
                        : "Mientras tanto, solo vos podés ver tu reseña.",
                ],
                Button: new BrandEmailButton("Ver mi reseña", _links.To($"/zonas/{Uri.EscapeDataString(review.ZoneSlug)}?tab=comunidad")),
                Footnote: "Te escribimos porque publicaste esta reseña."));
        }

        // Encode everything the author typed so it cannot inject HTML into the email.
        private static string AdministratorBody(DomainReview review, bool isEdit)
        {
            var score = review.OverallScore.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',');

            return BrandEmail.Render(new BrandEmailContent(
                Preheader: $"Reseña pendiente en {review.ZoneSlug}",
                Title: "Hay una reseña para revisar",
                Paragraphs:
                [
                    isEdit
                        ? "Una persona editó su reseña y necesita una nueva aprobación."
                        : "Una persona publicó una reseña y está pendiente de aprobación.",
                    $"<strong>Zona:</strong> {BrandEmail.Encode(review.ZoneSlug)}",
                    $"<strong>Puntaje general:</strong> {score} de 5",
                    $"<strong>Imágenes:</strong> {review.ImageNames.Count}",
                    $"<strong>Texto:</strong> {BrandEmail.Encode(review.Text)}",
                ],
                Footnote: "Aprobala o rechazala desde la bandeja de reseñas pendientes. Te escribimos porque sos administrador de ZonaMatch."));
        }

        private async Task TryAsync(Func<Task> action, Guid reviewId, string description, CancellationToken cancellationToken)
        {
            try
            {
                await action();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not {Action} about community review {ReviewId}.", description, reviewId);
            }
        }
    }
}
