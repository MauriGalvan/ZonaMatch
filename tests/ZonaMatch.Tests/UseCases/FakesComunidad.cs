using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases
{
    // Repositorios en memoria para los casos de uso de la comunidad. Las entidades se guardan por referencia,
    // como las que tienen seguimiento de cambios: SaveChangesAsync solo cuenta las llamadas.

    internal sealed class FixedTimeProvider(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    // Una sola zona, "villa-luro", que contiene todo punto con latitud menor a 0
    internal sealed class FakeZonaRepository : IZoneRepository
    {
        public const string Zona = "villa-luro";

        public Task<bool> ExistsAsync(string slug, CancellationToken cancellationToken = default) =>
            Task.FromResult(slug == Zona);

        public Task<bool> ContainsPointAsync(string slug, double latitud, double longitud, CancellationToken cancellationToken = default) =>
            Task.FromResult(slug == Zona && latitud < 0);

        public Task<string?> GetBySlugAsync(string slug, double tolerancia, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ZonaResumenDto>> SearchAsync(string fragmento, int limite, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    internal sealed class FakeAporteRepository : IContributionRepository
    {
        public List<Contribution> Aportes { get; } = [];
        public int Guardados { get; private set; }

        public Task<(int Aprobados, int Pendientes)> CountAsync(string zonaSlug, CancellationToken cancellationToken = default) =>
            Task.FromResult((
                Aportes.Count(a => a.ZonaSlug == zonaSlug && a.Estado == ContributionStatus.Aprobado),
                Aportes.Count(a => a.ZonaSlug == zonaSlug && a.Estado == ContributionStatus.Pendiente)));

        public Task<IReadOnlyList<Contribution>> ListPendingAsync(string zonaSlug, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Contribution>>(
                Aportes.Where(a => a.ZonaSlug == zonaSlug && a.Estado == ContributionStatus.Pendiente).ToList());

        public Task<Contribution?> GetAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Aportes.FirstOrDefault(a => a.ZonaSlug == zonaSlug && a.Id == id));

        public void Add(Contribution aporte) => Aportes.Add(aporte);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Guardados++;
            return Task.CompletedTask;
        }
    }

    internal sealed class FakeCommunityPoiRepository : ICommunityPoiRepository
    {
        public List<CommunityPoi> Pois { get; } = [];

        public Task<CommunityPoi?> GetAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Pois.FirstOrDefault(p => p.Id == id));

        public void Add(CommunityPoi poi) => Pois.Add(poi);
    }

    internal sealed class FakeResenaRepository : IReviewRepository
    {
        public List<Review> Resenas { get; } = [];
        public int Guardados { get; private set; }

        public Task<IReadOnlyList<Review>> ListByZoneAsync(string zonaSlug, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Review>>(
                Resenas.Where(r => r.ZonaSlug == zonaSlug).OrderByDescending(r => r.FechaCreacion).ToList());

        public Task<Review?> GetByUserAsync(string zonaSlug, Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Resenas.FirstOrDefault(r => r.ZonaSlug == zonaSlug && r.UsuarioId == usuarioId));

        public Task<Review?> GetAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Resenas.FirstOrDefault(r => r.ZonaSlug == zonaSlug && r.Id == id));

        public void Add(Review resena) => Resenas.Add(resena);

        public void Delete(Review resena) => Resenas.Remove(resena);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Guardados++;
            return Task.CompletedTask;
        }
    }

    internal sealed class FakePreguntaRepository : IQuestionRepository
    {
        public List<Question> Preguntas { get; } = [];

        public Task<IReadOnlyList<Question>> ListByZoneAsync(string zonaSlug, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Question>>(
                Preguntas.Where(p => p.ZonaSlug == zonaSlug).OrderByDescending(p => p.FechaCreacion).ToList());

        public Task<bool> ExistsAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Preguntas.Any(p => p.ZonaSlug == zonaSlug && p.Id == id));

        public Task<Question?> GetByIdAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Preguntas.FirstOrDefault(p => p.ZonaSlug == zonaSlug && p.Id == id));

        public void Add(Question pregunta) => Preguntas.Add(pregunta);

        public void Add(Answer respuesta) =>
            Preguntas.Single(p => p.Id == respuesta.PreguntaId).Respuestas.Add(respuesta);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    internal sealed class FakeSavedZoneRepository : ISavedZoneRepository
    {
        public Task<IReadOnlyList<SavedZone>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SavedZone?> GetAsync(Guid userId, string zoneSlug, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SavedZone>> GetForZoneAsync(
            string zoneSlug, Guid excludedUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SavedZone>>([]);

        public void Add(SavedZone savedZone) => throw new NotSupportedException();

        public void Remove(SavedZone savedZone) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    internal sealed class FakeNotificationRepository : INotificationRepository
    {
        public List<UserNotification> Notifications { get; } = [];

        public Task<IReadOnlyList<UserNotification>> GetForUserAsync(
            Guid userId, int limit, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UserNotification?> GetAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void AddRange(IEnumerable<UserNotification> notifications) => Notifications.AddRange(notifications);

        public Task MarkAllAsReadAsync(Guid userId, DateTimeOffset readAt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    internal sealed class FakeEmailService : IEmailService
    {
        public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendEmailAsync(string to, string subject, string body, IReadOnlyList<EmailAttachment> attachments, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
