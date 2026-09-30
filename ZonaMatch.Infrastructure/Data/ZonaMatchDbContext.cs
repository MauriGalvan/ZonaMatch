using Microsoft.EntityFrameworkCore;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Infrastructure.Data
{
    public class ZonaMatchDbContext : DbContext
    {
        public ZonaMatchDbContext(DbContextOptions<ZonaMatchDbContext> options)
            : base(options)
        {
        }

        public DbSet<Escuela> Escuelas => Set<Escuela>();
        public DbSet<EspacioVerde> EspaciosVerdes => Set<EspacioVerde>();
        public DbSet<Barrio> Barrios => Set<Barrio>();
        public DbSet<Zona> Zonas => Set<Zona>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasPostgresExtension("postgis");

            modelBuilder.Entity<Escuela>(e =>
            {
                e.ToTable("escuelas");
                e.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
                e.Property(x => x.Nivel).HasMaxLength(50);
                e.Property(x => x.Gestion).HasMaxLength(50);
                e.Property(x => x.Direccion).HasMaxLength(300);
                e.Property(x => x.Ubicacion)
                    .HasColumnType("geography (Point,4326)")
                    .IsRequired();
                e.HasIndex(x => x.Ubicacion).HasMethod("GIST");
            });

            modelBuilder.Entity<EspacioVerde>(e =>
            {
                e.ToTable("espacios_verdes");
                e.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
                e.Property(x => x.Tipo).HasMaxLength(50);
                e.Property(x => x.Ubicacion)
                    .HasColumnType("geography (Point,4326)")
                    .IsRequired();
                e.HasIndex(x => x.Ubicacion).HasMethod("GIST");
            });

            modelBuilder.Entity<Barrio>(e =>
            {
                e.ToTable("barrios");
                e.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
                e.Property(x => x.Comuna).HasMaxLength(50);
                e.Property(x => x.Geometria)
                    .HasColumnType("geometry (MultiPolygon,4326)")
                    .IsRequired();
                e.HasIndex(x => x.Geometria).HasMethod("GIST");
            });

            modelBuilder.Entity<Zona>(e =>
            {
                e.ToTable("zonas");
                e.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
                e.Property(x => x.Descripcion).HasMaxLength(500);
                e.Property(x => x.Geometria)
                    .HasColumnType("geometry (MultiPolygon,4326)")
                    .IsRequired();
                e.HasIndex(x => x.Geometria).HasMethod("GIST");
            });
        }
    }
}
