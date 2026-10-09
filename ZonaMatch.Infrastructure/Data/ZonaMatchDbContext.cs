using Microsoft.EntityFrameworkCore;
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
        public DbSet<Group> Groups => Set<Group>();

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
        }

        private static string ToSnakeCase(string name) =>
            string.Concat(name.Select((c, i) =>
                i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
    }
}
