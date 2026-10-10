using NetTopologySuite.Geometries;

namespace ZonaMatch.Domain.Entities
{
    public enum TipoAporte
    {
        // Un lugar que falta en el mapa
        PuntoNuevo,
        // Algo mal en un lugar del mapa
        Correccion
    }

    public enum EstadoAporte
    {
        Pendiente,
        Aprobado,
        Rechazado
    }

    public enum MotivoCorreccion
    {
        Cerro,
        Nombre,
        Ubicacion,
        Categoria,
        Otro
    }

    // Aporte de un vecino al mapa de una zona (tabla app.aportes). Queda pendiente hasta que otros vecinos
    // lo validan: los puntos y correcciones aprobados se aplican a los puntos de interes.
    public class Aporte
    {
        // Diferencia entre confirmaciones y rechazos que resuelve un aporte pendiente
        public const int DiferenciaParaResolver = 3;

        public Guid Id { get; set; } = Guid.CreateVersion7();

        public string ZonaSlug { get; set; } = string.Empty;

        public Guid UsuarioId { get; set; }
        public Usuario? Usuario { get; set; }

        public TipoAporte Tipo { get; set; }
        public EstadoAporte Estado { get; set; } = EstadoAporte.Pendiente;

        // Punto nuevo: sus datos. Correccion: el valor propuesto (Nombre, Categoria o Ubicacion, segun el Motivo).
        public string? Nombre { get; set; }
        public string? Categoria { get; set; }
        public Point? Ubicacion { get; set; }

        // Solo punto nuevo
        public string? Horario { get; set; }
        public string? Direccion { get; set; }
        public bool ViveOTrabajaEnZona { get; set; }

        // Solo correccion: el lugar corregido (id de OSM como "n123", o "a<id>" para un punto de vecinos)
        public string? PuntoInteresId { get; set; }
        public string? PuntoInteresNombre { get; set; }
        public MotivoCorreccion? Motivo { get; set; }
        public string? Comentario { get; set; }

        public DateTimeOffset FechaCreacion { get; set; }
        public DateTimeOffset? FechaResolucion { get; set; }

        public List<AporteValidacion> Validaciones { get; set; } = [];

        public int Confirmaciones => Validaciones.Count(v => v.Confirma);
        public int Rechazos => Validaciones.Count(v => !v.Confirma);

        // Registra (o cambia) el voto de un usuario y resuelve el aporte cuando se llega a la diferencia.
        // Quien lo llama verifica que el aporte este pendiente y que el usuario no sea su autor.
        public void Validar(Guid usuarioId, bool confirma, DateTimeOffset ahora)
        {
            var existente = Validaciones.FirstOrDefault(v => v.UsuarioId == usuarioId);
            if (existente is null)
                Validaciones.Add(new AporteValidacion { AporteId = Id, UsuarioId = usuarioId, Confirma = confirma, Fecha = ahora });
            else
            {
                existente.Confirma = confirma;
                existente.Fecha = ahora;
            }

            var diferencia = Confirmaciones - Rechazos;
            if (Math.Abs(diferencia) >= DiferenciaParaResolver)
            {
                Estado = diferencia > 0 ? EstadoAporte.Aprobado : EstadoAporte.Rechazado;
                FechaResolucion = ahora;
            }
        }
    }

    // Voto de un vecino sobre un aporte (tabla app.aportes_validaciones)
    public class AporteValidacion
    {
        public Guid AporteId { get; set; }
        public Guid UsuarioId { get; set; }
        public bool Confirma { get; set; }
        public DateTimeOffset Fecha { get; set; }
    }
}
