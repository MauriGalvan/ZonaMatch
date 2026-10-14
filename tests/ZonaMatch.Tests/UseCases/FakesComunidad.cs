using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases
{
    // Repositorios en memoria para los casos de uso de la comunidad. Las entidades se guardan por referencia,
    // como las que tienen seguimiento de cambios: GuardarCambiosAsync solo cuenta las llamadas.

    internal sealed class FixedTimeProvider(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    // Una sola zona, "villa-luro", que contiene todo punto con latitud menor a 0
    internal sealed class FakeZonaRepository : IZonaRepository
    {
        public const string Zona = "villa-luro";

        public Task<bool> ExisteAsync(string slug, CancellationToken cancellationToken = default) =>
            Task.FromResult(slug == Zona);

        public Task<bool> ContienePuntoAsync(string slug, double latitud, double longitud, CancellationToken cancellationToken = default) =>
            Task.FromResult(slug == Zona && latitud < 0);

        public Task<string?> GetPorSlugAsync(string slug, double tolerancia, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ZonaResumenDto>> BuscarAsync(string fragmento, int limite, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    internal sealed class FakeAporteRepository : IAporteRepository
    {
        public List<Aporte> Aportes { get; } = [];
        public int Guardados { get; private set; }

        public Task<(int Aprobados, int Pendientes)> ContarAsync(string zonaSlug, CancellationToken cancellationToken = default) =>
            Task.FromResult((
                Aportes.Count(a => a.ZonaSlug == zonaSlug && a.Estado == EstadoAporte.Aprobado),
                Aportes.Count(a => a.ZonaSlug == zonaSlug && a.Estado == EstadoAporte.Pendiente)));

        public Task<IReadOnlyList<Aporte>> ListarPendientesAsync(string zonaSlug, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Aporte>>(
                Aportes.Where(a => a.ZonaSlug == zonaSlug && a.Estado == EstadoAporte.Pendiente).ToList());

        public Task<Aporte?> ObtenerAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Aportes.FirstOrDefault(a => a.ZonaSlug == zonaSlug && a.Id == id));

        public void Agregar(Aporte aporte) => Aportes.Add(aporte);

        public Task GuardarCambiosAsync(CancellationToken cancellationToken = default)
        {
            Guardados++;
            return Task.CompletedTask;
        }
    }

    internal sealed class FakeResenaRepository : IResenaRepository
    {
        public List<Resena> Resenas { get; } = [];
        public int Guardados { get; private set; }

        public Task<IReadOnlyList<Resena>> ListarPorZonaAsync(string zonaSlug, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Resena>>(
                Resenas.Where(r => r.ZonaSlug == zonaSlug).OrderByDescending(r => r.FechaCreacion).ToList());

        public Task<Resena?> ObtenerDeUsuarioAsync(string zonaSlug, Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Resenas.FirstOrDefault(r => r.ZonaSlug == zonaSlug && r.UsuarioId == usuarioId));

        public Task<Resena?> ObtenerAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Resenas.FirstOrDefault(r => r.ZonaSlug == zonaSlug && r.Id == id));

        public void Agregar(Resena resena) => Resenas.Add(resena);

        public void Eliminar(Resena resena) => Resenas.Remove(resena);

        public Task GuardarCambiosAsync(CancellationToken cancellationToken = default)
        {
            Guardados++;
            return Task.CompletedTask;
        }
    }

    internal sealed class FakePreguntaRepository : IPreguntaRepository
    {
        public List<Pregunta> Preguntas { get; } = [];

        public Task<IReadOnlyList<Pregunta>> ListarPorZonaAsync(string zonaSlug, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Pregunta>>(
                Preguntas.Where(p => p.ZonaSlug == zonaSlug).OrderByDescending(p => p.FechaCreacion).ToList());

        public Task<bool> ExisteAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Preguntas.Any(p => p.ZonaSlug == zonaSlug && p.Id == id));

        public void Agregar(Pregunta pregunta) => Preguntas.Add(pregunta);

        public void Agregar(Respuesta respuesta) =>
            Preguntas.Single(p => p.Id == respuesta.PreguntaId).Respuestas.Add(respuesta);

        public Task GuardarCambiosAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
