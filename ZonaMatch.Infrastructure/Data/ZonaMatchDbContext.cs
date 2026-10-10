using Microsoft.EntityFrameworkCore;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Infrastructure.Data
{
    // Entities of the "geo" schema (external GIS data, see zonamatch.sql) are mapped here but excluded from
    // migrations: they are loaded from the dump, not created by EF Core. Tables of the application model go to "app".
    public class ZonaMatchDbContext : DbContext
    {
        public ZonaMatchDbContext(DbContextOptions<ZonaMatchDbContext> options)
            : base(options)
        {
        }

        public DbSet<Escuela> Escuelas => Set<Escuela>();
        public DbSet<Partido> Partidos => Set<Partido>();
        public DbSet<Comuna> Comunas => Set<Comuna>();
        public DbSet<Radio> Radios => Set<Radio>();
        public DbSet<MunicipioAlias> MunicipiosAlias => Set<MunicipioAlias>();
        public DbSet<Usuario> Usuarios => Set<Usuario>();
        public DbSet<Resena> Resenas => Set<Resena>();
        public DbSet<ResenaVotoUtil> ResenasVotosUtil => Set<ResenaVotoUtil>();
        public DbSet<Pregunta> Preguntas => Set<Pregunta>();
        public DbSet<Respuesta> Respuestas => Set<Respuesta>();
        public DbSet<Aporte> Aportes => Set<Aporte>();
        public DbSet<AporteValidacion> AportesValidaciones => Set<AporteValidacion>();
        public DbSet<Group> Groups => Set<Group>();
        public DbSet<GroupInvitation> GroupInvitations => Set<GroupInvitation>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema(ZonaMatchDbOptions.AppSchema);

            // The dump uses snake_case columns. Applied first so the explicit names below can override it.
            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
                property.SetColumnName(ToSnakeCase(property.Name));

            modelBuilder.Entity<Escuela>(e =>
            {
                e.ToTable("escuelas", ZonaMatchDbOptions.GeoSchema, t => t.ExcludeFromMigrations());
                e.HasKey(x => x.ClaveNatural);
                e.Property(x => x.Ubicacion).HasColumnName("geom").HasColumnType("geometry(Point,4326)");
            });

            modelBuilder.Entity<Partido>(e =>
            {
                e.ToTable("partidos", ZonaMatchDbOptions.GeoSchema, t => t.ExcludeFromMigrations());
                e.HasKey(x => x.Key);
                e.Property(x => x.Geometria).HasColumnName("geom").HasColumnType("geometry(MultiPolygon,4326)");
            });

            modelBuilder.Entity<Comuna>(e =>
            {
                e.ToTable("comunas", ZonaMatchDbOptions.GeoSchema, t => t.ExcludeFromMigrations());
                e.HasKey(x => x.Key);
                e.Property(x => x.Geometria).HasColumnName("geom").HasColumnType("geometry(MultiPolygon,4326)");
            });

            modelBuilder.Entity<Radio>(e =>
            {
                e.ToTable("radios", ZonaMatchDbOptions.GeoSchema, t => t.ExcludeFromMigrations());
                e.HasKey(x => x.Id);
                e.Property(x => x.NroRadio).HasColumnName("radio");
                e.Property(x => x.Geometria).HasColumnName("geom").HasColumnType("geometry(MultiPolygon,4326)");
            });

            modelBuilder.Entity<MunicipioAlias>(e =>
            {
                e.ToTable("municipio_alias", ZonaMatchDbOptions.GeoSchema, t => t.ExcludeFromMigrations());
                e.HasKey(x => x.MunicipioNombre);

                e.HasOne(x => x.Partido)
                    .WithMany(p => p.Aliases)
                    .HasForeignKey(x => x.PartidoKey);
            });

            // Application model: created by migrations in the "app" schema
            modelBuilder.Entity<Usuario>(e =>
            {
                e.ToTable("usuarios");
                e.HasKey(x => x.Id);
                e.Property(x => x.Email).HasMaxLength(254).IsRequired();
                e.HasIndex(x => x.Email).IsUnique();
                e.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
                e.Property(x => x.FechaNacimiento).HasColumnType("date");
                e.Property(x => x.FechaCreacion).HasColumnType("timestamptz");
            });

            // Comunidad de una zona (resenas, preguntas y aportes). Las zonas no son una tabla: se referencian
            // por slug (ver GET /Zonas/{slug}).
            modelBuilder.Entity<Resena>(e =>
            {
                e.ToTable("resenas", t =>
                {
                    t.HasCheckConstraint("ck_resenas_puntajes",
                        "puntaje_seguridad BETWEEN 1 AND 5 AND puntaje_transporte BETWEEN 1 AND 5 AND puntaje_conectividad BETWEEN 1 AND 5 " +
                        "AND puntaje_comercios BETWEEN 1 AND 5 AND puntaje_espacios_verdes BETWEEN 1 AND 5");
                });
                e.HasKey(x => x.Id);
                e.Property(x => x.ZonaSlug).HasMaxLength(200);
                e.HasIndex(x => new { x.ZonaSlug, x.UsuarioId }).IsUnique();
                e.Property(x => x.Texto).HasMaxLength(Longitudes.TextoResena);
                e.Property(x => x.Temas).HasColumnType("text[]");
                e.Property(x => x.FechaCreacion).HasColumnType("timestamptz");
                e.Property(x => x.FechaActualizacion).HasColumnType("timestamptz");
                e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.VotosUtil).WithOne().HasForeignKey(x => x.ResenaId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ResenaVotoUtil>(e =>
            {
                e.ToTable("resenas_votos_util");
                e.HasKey(x => new { x.ResenaId, x.UsuarioId });
                e.Property(x => x.Fecha).HasColumnType("timestamptz");
                e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Pregunta>(e =>
            {
                e.ToTable("preguntas");
                e.HasKey(x => x.Id);
                e.Property(x => x.ZonaSlug).HasMaxLength(200);
                e.HasIndex(x => x.ZonaSlug);
                e.Property(x => x.Texto).HasMaxLength(Longitudes.TextoPregunta);
                e.Property(x => x.FechaCreacion).HasColumnType("timestamptz");
                e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.Respuestas).WithOne().HasForeignKey(x => x.PreguntaId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Respuesta>(e =>
            {
                e.ToTable("respuestas");
                e.HasKey(x => x.Id);
                e.Property(x => x.Texto).HasMaxLength(Longitudes.TextoRespuesta);
                e.Property(x => x.FechaCreacion).HasColumnType("timestamptz");
                e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Aporte>(e =>
            {
                e.ToTable("aportes");
                e.HasKey(x => x.Id);
                e.Property(x => x.ZonaSlug).HasMaxLength(200);
                e.HasIndex(x => new { x.ZonaSlug, x.Estado });
                // Las consultas de puntos de interes buscan las correcciones aprobadas por lugar
                e.HasIndex(x => new { x.PuntoInteresId, x.Estado });
                // Enums como texto: legibles en la base y usables desde el SQL del mapa
                e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
                e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
                e.Property(x => x.Motivo).HasConversion<string>().HasMaxLength(20);
                e.Property(x => x.Nombre).HasMaxLength(Longitudes.NombreLugar);
                e.Property(x => x.Categoria).HasMaxLength(40);
                e.Property(x => x.Ubicacion).HasColumnType("geometry(Point,4326)");
                e.Property(x => x.Horario).HasMaxLength(Longitudes.Horario);
                e.Property(x => x.Direccion).HasMaxLength(Longitudes.Direccion);
                e.Property(x => x.PuntoInteresId).HasMaxLength(40);
                e.Property(x => x.PuntoInteresNombre).HasMaxLength(Longitudes.Direccion);
                e.Property(x => x.Comentario).HasMaxLength(Longitudes.Comentario);
                e.Property(x => x.FechaCreacion).HasColumnType("timestamptz");
                e.Property(x => x.FechaResolucion).HasColumnType("timestamptz");
                e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.Validaciones).WithOne().HasForeignKey(x => x.AporteId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AporteValidacion>(e =>
            {
                e.ToTable("aportes_validaciones");
                e.HasKey(x => new { x.AporteId, x.UsuarioId });
                e.Property(x => x.Fecha).HasColumnType("timestamptz");
                e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<GroupInvitation>()
                .Property(i => i.Status)
                .HasConversion<string>();
        }

        private static string ToSnakeCase(string name) =>
            string.Concat(name.Select((c, i) =>
                i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
    }
}
