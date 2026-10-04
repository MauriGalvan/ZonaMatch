using NetTopologySuite.Geometries;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Domain.Entities
{
    // School from the education registry (table geo.escuelas, loaded from the dump)
    public class Escuela : IUbicacionPunto
    {
        public string ClaveNatural { get; set; } = string.Empty;
        public string? Fuente { get; set; }
        public string? Cueanexo { get; set; }
        public string? Cue { get; set; }
        public int? Anexo { get; set; }
        public string? Clave { get; set; }
        public string? NroEscuela { get; set; }
        public string? Cui { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? TipoOrganizacion { get; set; }

        // e.g. "Nivel Inicial"
        public string? Nivel { get; set; }
        public int? IdNivel { get; set; }

        // e.g. "Estatal", "Privado"
        public string? Sector { get; set; }
        public int? IdSector { get; set; }
        public string? Dependencia { get; set; }
        public int? IdDependencia { get; set; }
        public string? Modalidad { get; set; }
        public int? IdModalidad { get; set; }
        public int? IdTipoOrganizacion { get; set; }

        public string? Distrito { get; set; }
        public string? IdDistrito { get; set; }

        // Logical reference to Partido.Key (no FK in the dump)
        public string? PartidoKey { get; set; }
        public string? Localidad { get; set; }
        public string? IdLocalidad { get; set; }
        public string? SeccionElectoral { get; set; }
        public string? Region { get; set; }
        public string? RegionEducativa { get; set; }

        public string? Calle { get; set; }
        public string? Nro { get; set; }
        public string? CalleLateralDerecha { get; set; }
        public string? CalleLateralIzquierda { get; set; }
        public string? CodigoPostal { get; set; }
        public string? Direccion { get; set; }
        public string? Telefono { get; set; }
        public string? Email { get; set; }
        public string? Rpv { get; set; }
        public string? EscSede { get; set; }
        public string? SedeAxoExt { get; set; }
        public string? Area { get; set; }
        public string? Ambito { get; set; }
        public string? Desfavorabilidad { get; set; }
        public string? Subvencion { get; set; }
        public string? Categoria { get; set; }
        public string? Turnos { get; set; }
        public int? Secciones { get; set; }
        public int? Matricula { get; set; }
        public int? Varones { get; set; }
        public int? Mujeres { get; set; }
        public string? Periodo { get; set; }
        public double? Latitud { get; set; }
        public double? Longitud { get; set; }

        // Column "geom": geometry(Point,4326)
        public Point Ubicacion { get; set; } = null!;
    }
}
