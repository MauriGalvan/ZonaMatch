using Microsoft.EntityFrameworkCore;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data.Converters;

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

        public DbSet<School> Schools => Set<School>();
        public DbSet<District> Districts => Set<District>();
        public DbSet<Commune> Communes => Set<Commune>();
        public DbSet<CensusTract> CensusTracts => Set<CensusTract>();
        public DbSet<MunicipalityAlias> MunicipalityAliases => Set<MunicipalityAlias>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Review> Reviews => Set<Review>();
        public DbSet<ReviewHelpfulVote> ReviewHelpfulVotes => Set<ReviewHelpfulVote>();
        public DbSet<Question> Questions => Set<Question>();
        public DbSet<Answer> Answers => Set<Answer>();
        public DbSet<Contribution> Contributions => Set<Contribution>();
        public DbSet<ContributionVote> ContributionVotes => Set<ContributionVote>();
        public DbSet<CommunityPoi> CommunityPois => Set<CommunityPoi>();
        public DbSet<Group> Groups => Set<Group>();
        public DbSet<GroupInvitation> GroupInvitations => Set<GroupInvitation>();
        public DbSet<Context> Contexts => Set<Context>();
        public DbSet<Analysis> Analyses => Set<Analysis>();
        public DbSet<AnalysisFollowUp> AnalysisFollowUps => Set<AnalysisFollowUp>();
        public DbSet<UserFeedbackPreferences> UserFeedbackPreferences => Set<UserFeedbackPreferences>();
        public DbSet<AnalysisZone> AnalysisZones => Set<AnalysisZone>();
        public DbSet<Person> People => Set<Person>();
        public DbSet<ContextPlace> ContextPlaces => Set<ContextPlace>();
        public DbSet<ContextCriterion> ContextCriteria => Set<ContextCriterion>();
        public DbSet<Criterion> Criteria => Set<Criterion>();
        public DbSet<ZoneMarket> ZoneMarkets => Set<ZoneMarket>();
        public DbSet<ZoneMarketRooms> ZoneMarketRooms => Set<ZoneMarketRooms>();
        public DbSet<SavedZone> SavedZones => Set<SavedZone>();
        public DbSet<UserNotification> Notifications => Set<UserNotification>();
        public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
        public DbSet<GuestReportSend> GuestReportSends => Set<GuestReportSend>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema(ZonaMatchDbOptions.AppSchema);

            // The dump uses snake_case columns. Applied first so the explicit names below can override it.
            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
                property.SetColumnName(ToSnakeCase(property.Name));

            modelBuilder.Entity<School>(e =>
            {
                e.ToTable("escuelas", ZonaMatchDbOptions.GeoSchema, t => t.ExcludeFromMigrations());
                e.HasKey(x => x.NaturalKey);
                e.Property(x => x.NaturalKey).HasColumnName("clave_natural");
                e.Property(x => x.Source).HasColumnName("fuente");
                e.Property(x => x.CueAnnex).HasColumnName("cueanexo");
                e.Property(x => x.Cue).HasColumnName("cue");
                e.Property(x => x.Annex).HasColumnName("anexo");
                e.Property(x => x.SchoolCode).HasColumnName("clave");
                e.Property(x => x.SchoolNumber).HasColumnName("nro_escuela");
                e.Property(x => x.Cui).HasColumnName("cui");
                e.Property(x => x.Name).HasColumnName("nombre").IsRequired();
                e.Property(x => x.OrganizationType).HasColumnName("tipo_organizacion");
                e.Property(x => x.Level).HasColumnName("nivel");
                e.Property(x => x.LevelId).HasColumnName("id_nivel");
                e.Property(x => x.Sector).HasColumnName("sector");
                e.Property(x => x.SectorId).HasColumnName("id_sector");
                e.Property(x => x.ManagingAuthority).HasColumnName("dependencia");
                e.Property(x => x.ManagingAuthorityId).HasColumnName("id_dependencia");
                e.Property(x => x.Modality).HasColumnName("modalidad");
                e.Property(x => x.ModalityId).HasColumnName("id_modalidad");
                e.Property(x => x.OrganizationTypeId).HasColumnName("id_tipo_organizacion");
                e.Property(x => x.DistrictName).HasColumnName("distrito");
                e.Property(x => x.DistrictId).HasColumnName("id_distrito");
                e.Property(x => x.DistrictKey).HasColumnName("partido_key");
                e.Property(x => x.Locality).HasColumnName("localidad");
                e.Property(x => x.LocalityId).HasColumnName("id_localidad");
                e.Property(x => x.ElectoralSection).HasColumnName("seccion_electoral");
                e.Property(x => x.Region).HasColumnName("region");
                e.Property(x => x.EducationalRegion).HasColumnName("region_educativa");
                e.Property(x => x.Street).HasColumnName("calle");
                e.Property(x => x.Number).HasColumnName("nro");
                e.Property(x => x.RightSideStreet).HasColumnName("calle_lateral_derecha");
                e.Property(x => x.LeftSideStreet).HasColumnName("calle_lateral_izquierda");
                e.Property(x => x.PostalCode).HasColumnName("codigo_postal");
                e.Property(x => x.Address).HasColumnName("direccion");
                e.Property(x => x.Phone).HasColumnName("telefono");
                e.Property(x => x.Email).HasColumnName("email");
                e.Property(x => x.Rpv).HasColumnName("rpv");
                e.Property(x => x.SchoolSite).HasColumnName("esc_sede");
                e.Property(x => x.SiteYearExtension).HasColumnName("sede_axo_ext");
                e.Property(x => x.Area).HasColumnName("area");
                e.Property(x => x.Scope).HasColumnName("ambito");
                e.Property(x => x.DisadvantageLevel).HasColumnName("desfavorabilidad");
                e.Property(x => x.Subsidy).HasColumnName("subvencion");
                e.Property(x => x.Category).HasColumnName("categoria");
                e.Property(x => x.Shifts).HasColumnName("turnos");
                e.Property(x => x.Sections).HasColumnName("secciones");
                e.Property(x => x.Enrollment).HasColumnName("matricula");
                e.Property(x => x.MaleStudents).HasColumnName("varones");
                e.Property(x => x.FemaleStudents).HasColumnName("mujeres");
                e.Property(x => x.Period).HasColumnName("periodo");
                e.Property(x => x.Latitude).HasColumnName("latitud");
                e.Property(x => x.Longitude).HasColumnName("longitud");
                e.Property(x => x.Location).HasColumnName("geom").HasColumnType("geometry(Point,4326)");
            });

            modelBuilder.Entity<District>(e =>
            {
                e.ToTable("partidos", ZonaMatchDbOptions.GeoSchema, t => t.ExcludeFromMigrations());
                e.HasKey(x => x.Key);
                e.Property(x => x.MunicipalityId).HasColumnName("municipio_id");
                e.Property(x => x.Name).HasColumnName("nombre");
                e.Property(x => x.Province).HasColumnName("provincia");
                e.Property(x => x.CensusCode).HasColumnName("censo_codigo");
                e.Property(x => x.Geometry).HasColumnName("geom").HasColumnType("geometry(MultiPolygon,4326)");
            });

            modelBuilder.Entity<Commune>(e =>
            {
                e.ToTable("comunas", ZonaMatchDbOptions.GeoSchema, t => t.ExcludeFromMigrations());
                e.HasKey(x => x.Key);
                e.Property(x => x.CabaId).HasColumnName("caba_id");
                e.Property(x => x.Name).HasColumnName("nombre");
                e.Property(x => x.Province).HasColumnName("provincia");
                e.Property(x => x.CensusCode).HasColumnName("censo_codigo");
                e.Property(x => x.Geometry).HasColumnName("geom").HasColumnType("geometry(MultiPolygon,4326)");
            });

            modelBuilder.Entity<CensusTract>(e =>
            {
                e.ToTable("radios", ZonaMatchDbOptions.GeoSchema, t => t.ExcludeFromMigrations());
                e.HasKey(x => x.Id);
                e.Property(x => x.Source).HasColumnName("origen");
                e.Property(x => x.DepartmentCode).HasColumnName("depto");
                e.Property(x => x.CommuneCode).HasColumnName("comuna");
                e.Property(x => x.FractionCode).HasColumnName("fraccion");
                e.Property(x => x.RadioNumber).HasColumnName("radio");
                e.Property(x => x.Geometry).HasColumnName("geom").HasColumnType("geometry(MultiPolygon,4326)");
            });

            modelBuilder.Entity<MunicipalityAlias>(e =>
            {
                e.ToTable("municipio_alias", ZonaMatchDbOptions.GeoSchema, t => t.ExcludeFromMigrations());
                e.HasKey(x => x.MunicipalityName);
                e.Property(x => x.MunicipalityName).HasColumnName("municipio_nombre");
                e.Property(x => x.DistrictKey).HasColumnName("partido_key");

                e.HasOne(x => x.District)
                    .WithMany(p => p.Aliases)
                    .HasForeignKey(x => x.DistrictKey);
            });

            // Application model: created by migrations in the "app" schema
            modelBuilder.Entity<User>(e =>
            {
                e.ToTable("usuarios", t =>
                    t.HasCheckConstraint("ck_usuarios_acceso_inversor", "acceso_inversor IN ('solicitado', 'habilitado')"));
                e.HasKey(x => x.Id);
                e.Property(x => x.Email).HasMaxLength(254).IsRequired();
                e.HasIndex(x => x.Email).IsUnique();
                e.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
                e.Property(x => x.DateOfBirth).HasColumnName("fecha_nacimiento").HasColumnType("date");
                e.Property(x => x.CreatedAt).HasColumnName("fecha_creacion").HasColumnType("timestamptz");
                e.Property(x => x.IsAdministrator).HasColumnName("es_administrador");
                e.Property(x => x.InvestorAccess).HasColumnName("acceso_inversor").HasMaxLength(20);
                e.Property(x => x.InvestorAccessRequestedAt).HasColumnName("acceso_inversor_solicitado").HasColumnType("timestamptz");
                e.Property(x => x.InvestorAccessGrantedAt).HasColumnName("acceso_inversor_habilitado").HasColumnType("timestamptz");
                e.Ignore(x => x.HasInvestorAccess);
            });

            modelBuilder.Entity<UserFeedbackPreferences>(e =>
            {
                e.ToTable("user_feedback_preferences");
                e.HasKey(x => x.UserId);
                // An invitation must not overwrite a simultaneous opt-out or a newer waiting period.
                e.Property(x => x.SurveysEnabled).IsConcurrencyToken();
                e.Property(x => x.NextInvitationAt).HasColumnType("timestamptz").IsConcurrencyToken();
                e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AnalysisFollowUp>(e =>
            {
                e.ToTable("analysis_follow_ups", t =>
                {
                    t.HasCheckConstraint("ck_analysis_follow_ups_helpfulness", "helpfulness IS NULL OR helpfulness BETWEEN 1 AND 5");
                    t.HasCheckConstraint("ck_analysis_follow_ups_outcome", "outcome IS NULL OR outcome IN ('still-searching', 'visited-recommended-zone', 'chose-recommended-zone', 'chose-another-zone', 'stopped-searching')");
                    t.HasCheckConstraint("ck_analysis_follow_ups_completion", "(completed_at IS NULL AND outcome IS NULL AND helpfulness IS NULL) OR (completed_at IS NOT NULL AND outcome IS NOT NULL AND helpfulness IS NOT NULL)");
                });
                e.HasKey(x => x.Id);
                e.Property(x => x.Outcome).HasMaxLength(32);
                e.Property(x => x.Comment).HasMaxLength(AnalysisFollowUpRules.MaximumCommentLength);
                e.Property(x => x.InvitedAt).HasColumnType("timestamptz");
                e.Property(x => x.PostponedUntil).HasColumnType("timestamptz");
                // A concurrent response must not overwrite the first completed answer.
                e.Property(x => x.CompletedAt).HasColumnType("timestamptz").IsConcurrencyToken();
                e.HasIndex(x => x.AnalysisId).IsUnique();
                e.HasIndex(x => new { x.UserId, x.CompletedAt, x.PostponedUntil });
                e.HasOne(x => x.Analysis).WithMany().HasForeignKey(x => x.AnalysisId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            // Search criteria: the user's profile or the snapshot saved with an analysis.
            modelBuilder.Entity<Context>(e =>
            {
                e.ToTable("contextos", t =>
                {
                    t.HasCheckConstraint("ck_contextos_tipo", "tipo IN ('perfil', 'analisis', 'inversor', 'analisis-inversor')");
                    t.HasCheckConstraint("ck_contextos_operacion", "operacion IN ('alquiler', 'compra')");
                    t.HasCheckConstraint("ck_contextos_tipo_vivienda", "tipo_vivienda IS NULL OR tipo_vivienda IN ('apartment', 'house', 'ph')");
                    t.HasCheckConstraint("ck_contextos_superficie_m2", "superficie_m2 IS NULL OR superficie_m2 BETWEEN 10 AND 2000");
                    t.HasCheckConstraint("ck_contextos_ambientes", "ambientes IS NULL OR ambientes BETWEEN 1 AND 5");
                });
                e.HasKey(x => x.Id);
                e.Property(x => x.Type).HasColumnName("tipo").HasMaxLength(10).IsRequired();
                e.Property(x => x.PeopleCount).HasColumnName("personas").IsRequired();
                e.Property(x => x.Pets).HasColumnName("mascotas").HasColumnType("text[]");
                e.Property(x => x.Mobility).HasColumnName("movilidad").HasColumnType("text[]");
                e.Property(x => x.MaximumRent).HasColumnName("alquiler_maximo").HasMaxLength(30);
                e.Property(x => x.Operation).HasColumnName("operacion").HasMaxLength(10).IsRequired().HasDefaultValue("alquiler");
                e.Property(x => x.MaximumPurchasePriceUsd).HasColumnName("compra_maxima_usd").HasColumnType("numeric(12,0)");
                e.Property(x => x.HousingType).HasColumnName("tipo_vivienda").HasMaxLength(20);
                e.Property(x => x.AreaM2).HasColumnName("superficie_m2");
                e.Property(x => x.Rooms).HasColumnName("ambientes");
                e.Property(x => x.CreditEligible).HasColumnName("apto_credito").HasDefaultValue(false);
                e.Property(x => x.CreatedAt).HasColumnName("fecha_creacion").HasColumnType("timestamptz");
                e.Property(x => x.UpdatedAt).HasColumnName("fecha_actualizacion").HasColumnType("timestamptz");
                e.Property(x => x.UserId).HasColumnName("usuario_id");
                // Each user has one profile and can have any number of analysis snapshots.
                e.HasIndex(x => x.UserId)
                    .IsUnique()
                    .HasFilter("tipo = 'perfil'")
                    .HasDatabaseName("ux_contextos_perfil_usuario");
                // One investor profile per user as well
                e.HasIndex(x => x.UserId, "ux_contextos_inversor_usuario")
                    .IsUnique()
                    .HasFilter("tipo = 'inversor'");
                e.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.People)
                    .WithOne(p => p.Context)
                    .HasForeignKey(p => p.ContextId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.Places)
                    .WithOne(p => p.Context)
                    .HasForeignKey(p => p.ContextId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.Criteria)
                    .WithOne()
                    .HasForeignKey(x => x.ContextId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Saved analysis: compared zones and the criteria snapshot used to calculate it.
            modelBuilder.Entity<Analysis>(e =>
            {
                e.ToTable("analisis");
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).HasColumnName("nombre").HasMaxLength(50).IsRequired();
                e.Property(x => x.GroupScoresJson).HasColumnName("puntajes_en_grupo_json").HasColumnType("jsonb");
                e.Property(x => x.ResultJson).HasColumnName("resultado_json").HasColumnType("jsonb");
                e.Property(x => x.ResultGeneratedAt).HasColumnName("fecha_resultado").HasColumnType("timestamptz");
                e.Property(x => x.IsFavorite).HasColumnName("es_favorito").HasDefaultValue(false);
                e.Property(x => x.CreatedAt).HasColumnName("fecha_creacion").HasColumnType("timestamptz");
                e.Property(x => x.UpdatedAt).HasColumnName("fecha_actualizacion").HasColumnType("timestamptz");
                e.Property(x => x.UserId).HasColumnName("usuario_id");
                e.Property(x => x.ContextId).HasColumnName("contexto_id");
                e.Property(x => x.OriginalAnalysisId).HasColumnName("analisis_origen_id");
                e.Property(x => x.DeletedAt).HasColumnName("fecha_eliminacion").HasColumnType("timestamptz");
                e.HasIndex(x => x.UserId);
                e.HasIndex(x => new { x.UserId, x.DeletedAt });
                e.HasIndex(x => x.DeletedAt);
                e.HasIndex(x => x.OriginalAnalysisId);
                e.HasIndex(x => x.ContextId).IsUnique();
                e.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                // Each analysis has its own criteria snapshot; both are deleted together (see DeleteAnalysis).
                e.HasOne(x => x.Context)
                    .WithMany()
                    .HasForeignKey(x => x.ContextId)
                    .OnDelete(DeleteBehavior.Cascade);
                // Deleting an original analysis also deletes its versions.
                e.HasOne<Analysis>()
                    .WithMany()
                    .HasForeignKey(x => x.OriginalAnalysisId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.Zones)
                    .WithOne()
                    .HasForeignKey(x => x.AnalysisId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AnalysisZone>(e =>
            {
                e.ToTable("analisis_zonas");
                e.HasKey(x => new { x.AnalysisId, x.Order });
                e.Property(x => x.AnalysisId).HasColumnName("analisis_id");
                e.Property(x => x.Order).HasColumnName("orden");
                e.Property(x => x.ZoneSlug).HasColumnName("zona_slug").HasMaxLength(100).IsRequired();
                e.Property(x => x.Name).HasColumnName("nombre").HasMaxLength(150).IsRequired();
                e.Property(x => x.IsInitial).HasColumnName("es_inicial");
                e.Property(x => x.IsAdded).HasColumnName("es_agregada").HasDefaultValue(false);
                e.Property(x => x.OriginLatitude).HasColumnName("origen_latitud");
                e.Property(x => x.OriginLongitude).HasColumnName("origen_longitud");
            });

            modelBuilder.Entity<SavedZone>(e =>
            {
                e.ToTable("saved_zones");
                e.HasKey(x => new { x.UserId, x.ZoneSlug });
                e.Property(x => x.ZoneSlug).HasMaxLength(100).IsRequired();
                e.Property(x => x.Name).HasMaxLength(150).IsRequired();
                e.Property(x => x.Subtitle).HasMaxLength(300).IsRequired();
                e.Property(x => x.SavedAt).HasColumnType("timestamptz");
                e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PasswordResetToken>(e =>
            {
                e.ToTable("password_reset_tokens");
                e.HasKey(x => x.Id);
                e.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
                e.Property(x => x.CreatedAt).HasColumnType("timestamptz");
                e.Property(x => x.ExpiresAt).HasColumnType("timestamptz");
                e.Property(x => x.UsedAt).HasColumnType("timestamptz");
                e.HasIndex(x => x.TokenHash).IsUnique();
                e.HasIndex(x => new { x.UserId, x.CreatedAt });
                e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<GuestReportSend>(e =>
            {
                e.ToTable("guest_report_sends");
                e.HasKey(x => x.Id);
                e.Property(x => x.RecipientHash).HasMaxLength(64).IsRequired();
                e.Property(x => x.SentAt).HasColumnType("timestamptz");
                e.HasIndex(x => new { x.RecipientHash, x.SentAt });
            });

            modelBuilder.Entity<UserNotification>(e =>
            {
                e.ToTable("notifications");
                e.HasKey(x => x.Id);
                e.Property(x => x.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
                e.Property(x => x.Title).HasMaxLength(150).IsRequired();
                e.Property(x => x.Message).HasMaxLength(500).IsRequired();
                e.Property(x => x.Link).HasMaxLength(500).IsRequired();
                e.Property(x => x.CreatedAt).HasColumnType("timestamptz");
                e.Property(x => x.ReadAt).HasColumnType("timestamptz");
                e.HasIndex(x => new { x.UserId, x.ReadAt, x.CreatedAt });
                e.HasIndex(x => x.ReviewId);
                e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Person>(e =>
            {
                e.ToTable("personas");
                e.HasKey(x => x.Id);
                e.Property(x => x.Order).HasColumnName("orden");
                e.Property(x => x.Name).HasColumnName("nombre").HasMaxLength(50);
                e.Property(x => x.AgeRange).HasColumnName("edad").HasMaxLength(40);
                e.Property(x => x.ContextId).HasColumnName("contexto_id");
                e.HasIndex(x => new { x.ContextId, x.Order });
                e.HasOne(x => x.Context)
                    .WithMany(c => c.People)
                    .HasForeignKey(x => x.ContextId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ContextPlace>(e =>
            {
                e.ToTable("puntos_interes");
                e.HasKey(x => x.Id);
                e.Property(x => x.Order).HasColumnName("orden");
                e.Property(x => x.Name).HasColumnName("nombre").HasMaxLength(100).IsRequired();
                e.Property(x => x.Address).HasColumnName("direccion").HasMaxLength(200);
                e.Property(x => x.Type).HasColumnName("tipo").HasMaxLength(30);
                e.Property(x => x.WhoGoes).HasColumnName("quien_va").HasMaxLength(50);
                e.Property(x => x.Frequency).HasColumnName("frecuencia").HasMaxLength(20);
                e.Property(x => x.Priority).HasColumnName("prioridad").HasMaxLength(20);
                e.Property(x => x.MaximumTravelTime).HasColumnName("tiempo_max").HasMaxLength(10);
                e.Property(x => x.ContextId).HasColumnName("contexto_id");
                e.HasIndex(x => new { x.ContextId, x.Order });
                e.HasOne(x => x.Context)
                    .WithMany(c => c.Places)
                    .HasForeignKey(x => x.ContextId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ContextCriterion>(e =>
            {
                e.ToTable("contexto_criterios", t =>
                    t.HasCheckConstraint("ck_contexto_criterios_prioridad", "prioridad IN ('baja', 'media', 'alta')"));
                e.HasKey(x => new { x.ContextId, x.CriterionId });
                e.Property(x => x.ContextId).HasColumnName("contexto_id");
                e.Property(x => x.CriterionId).HasColumnName("criterio_id").HasMaxLength(40);
                e.Property(x => x.Priority).HasColumnName("prioridad").HasMaxLength(10).IsRequired();
                e.HasOne(x => x.Context)
                    .WithMany(c => c.Criteria)
                    .HasForeignKey(x => x.ContextId)
                    .OnDelete(DeleteBehavior.Cascade);
                // A criterion should not be deleted from the catalog, but if it is,
                // the analysis row must remain unchanged.
                e.HasOne(x => x.Criterion)
                    .WithMany()
                    .HasForeignKey(x => x.CriterionId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Criterion>(e =>
            {
                e.ToTable("criterios");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).HasMaxLength(40);
                e.Property(x => x.Name).HasColumnName("nombre").HasMaxLength(50).IsRequired();
                e.Property(x => x.Description).HasColumnName("descripcion").HasMaxLength(200).IsRequired();
                e.Property(x => x.IconCssClass).HasColumnName("icono").HasMaxLength(40).IsRequired();
                e.Property(x => x.Order).HasColumnName("orden");
                e.Property(x => x.IsActive).HasColumnName("activo");
                e.Property(x => x.ForHousehold).HasColumnName("perfil_hogar").HasDefaultValue(true);
                e.Property(x => x.ForInvestor).HasColumnName("perfil_inversor").HasDefaultValue(false);
                e.HasIndex(x => x.Order);
            });

            // Real estate market per zone, operation and property type, loaded by the ETL (etl/zonaprop/)
            modelBuilder.Entity<ZoneMarket>(e =>
            {
                e.Property(x => x.PriceDeciles).HasColumnType("numeric[]");
                e.Property(x => x.PriceM2Deciles).HasColumnType("numeric[]");
                e.Property(x => x.PriceShares).HasColumnType("numeric[]");
                e.Property(x => x.PriceM2Shares).HasColumnType("numeric[]");
                e.Property(x => x.CreditPrices).HasColumnType("numeric[]");
                e.Property(x => x.CreditShares).HasColumnType("numeric[]");
                e.ToTable("zone_market", t =>
                {
                    t.HasCheckConstraint("ck_zone_market_operation", "operation IN ('sale', 'rent')");
                    t.HasCheckConstraint("ck_zone_market_property_type",
                        "property_type IN ('apartment', 'house', 'ph', 'all')");
                    t.HasCheckConstraint("ck_zone_market_currency", "currency IN ('ARS', 'USD')");
                    t.HasCheckConstraint("ck_zone_market_counts", "listings >= 0 AND sample_size >= 0");
                    t.HasCheckConstraint("ck_zone_market_prices", "average_price >= 0 AND median_price >= 0");
                });
                e.HasKey(x => new { x.ZoneSlug, x.Operation, x.PropertyType });
                e.Property(x => x.ZoneSlug).HasMaxLength(100);
                e.Property(x => x.Operation).HasMaxLength(10);
                e.Property(x => x.PropertyType).HasMaxLength(20);
                e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
                e.Property(x => x.AveragePrice).HasColumnType("numeric(14,0)");
                e.Property(x => x.MedianPrice).HasColumnType("numeric(14,0)");
                e.Property(x => x.AveragePriceM2).HasColumnType("numeric(12,0)");
                e.Property(x => x.MedianPriceM2).HasColumnType("numeric(12,0)");
                e.Property(x => x.AverageExpensesArs).HasColumnType("numeric(12,0)");
                e.Property(x => x.MedianCoveredM2).HasColumnType("numeric(8,0)");
                e.Property(x => x.MedianTotalM2).HasColumnType("numeric(8,0)");
                e.Property(x => x.CreditEligibleShare).HasColumnType("numeric(5,4)");
                e.Property(x => x.OwnerShare).HasColumnType("numeric(5,4)");
                e.Property(x => x.PetsAllowedShare).HasColumnType("numeric(5,4)");
                e.Property(x => x.Source).HasMaxLength(30).IsRequired();
                e.Property(x => x.InheritedFrom).HasMaxLength(100);
                e.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
            });

            // The same market per number of rooms (1 to 4, 5 = 5 or more), loaded by the ETL (etl/zonaprop/)
            modelBuilder.Entity<ZoneMarketRooms>(e =>
            {
                e.Property(x => x.PriceDeciles).HasColumnType("numeric[]");
                e.Property(x => x.PriceM2Deciles).HasColumnType("numeric[]");
                e.Property(x => x.PriceShares).HasColumnType("numeric[]");
                e.Property(x => x.PriceM2Shares).HasColumnType("numeric[]");
                e.Property(x => x.CreditPrices).HasColumnType("numeric[]");
                e.Property(x => x.CreditShares).HasColumnType("numeric[]");
                e.ToTable("zone_market_rooms", t =>
                {
                    t.HasCheckConstraint("ck_zone_market_rooms_operation", "operation IN ('sale', 'rent')");
                    t.HasCheckConstraint("ck_zone_market_rooms_property_type",
                        "property_type IN ('apartment', 'house', 'ph', 'all')");
                    t.HasCheckConstraint("ck_zone_market_rooms_rooms", "rooms BETWEEN 1 AND 5");
                    t.HasCheckConstraint("ck_zone_market_rooms_currency", "currency IN ('ARS', 'USD')");
                    t.HasCheckConstraint("ck_zone_market_rooms_counts", "listings >= 0 AND sample_size >= 0");
                });
                e.HasKey(x => new { x.ZoneSlug, x.Operation, x.PropertyType, x.Rooms });
                e.Property(x => x.ZoneSlug).HasMaxLength(100);
                e.Property(x => x.Operation).HasMaxLength(10);
                e.Property(x => x.PropertyType).HasMaxLength(20);
                e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
                e.Property(x => x.AveragePrice).HasColumnType("numeric(14,0)");
                e.Property(x => x.MedianPrice).HasColumnType("numeric(14,0)");
                e.Property(x => x.AveragePriceM2).HasColumnType("numeric(12,0)");
                e.Property(x => x.MedianPriceM2).HasColumnType("numeric(12,0)");
                e.Property(x => x.AverageExpensesArs).HasColumnType("numeric(12,0)");
                e.Property(x => x.MedianCoveredM2).HasColumnType("numeric(8,0)");
                e.Property(x => x.MedianTotalM2).HasColumnType("numeric(8,0)");
                e.Property(x => x.Source).HasMaxLength(30).IsRequired();
                e.Property(x => x.InheritedFrom).HasMaxLength(100);
                e.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
            });

            // Zone community (reviews, questions, and contributions). Zones are not stored in an application table;
            // they are referenced by slug (see GET /api/v1/zones/{slug}).
            modelBuilder.Entity<Review>(e =>
            {
                e.ToTable("resenas", t =>
                {
                    t.HasCheckConstraint("ck_resenas_puntajes",
                        "puntaje_seguridad BETWEEN 1 AND 5 AND puntaje_transporte BETWEEN 1 AND 5 AND puntaje_conectividad BETWEEN 1 AND 5 " +
                        "AND puntaje_comercios BETWEEN 1 AND 5 AND puntaje_espacios_verdes BETWEEN 1 AND 5");
                });
                e.HasKey(x => x.Id);
                e.Property(x => x.ZoneSlug).HasColumnName("zona_slug").HasMaxLength(200);
                e.Property(x => x.UserId).HasColumnName("usuario_id");
                e.Property(x => x.SafetyScore).HasColumnName("puntaje_seguridad");
                e.Property(x => x.TransitScore).HasColumnName("puntaje_transporte");
                e.Property(x => x.ConnectivityScore).HasColumnName("puntaje_conectividad");
                e.Property(x => x.CommerceScore).HasColumnName("puntaje_comercios");
                e.Property(x => x.GreenSpaceScore).HasColumnName("puntaje_espacios_verdes");
                e.Property(x => x.Text).HasColumnName("texto").HasMaxLength(TextLimits.ReviewText);
                e.Property(x => x.YearsInZone).HasColumnName("anios_en_zona");
                e.Property(x => x.Topics).HasColumnName("temas").HasColumnType("text[]");
                e.Property(x => x.ImageNames).HasColumnName("imagenes").HasColumnType("text[]");
                e.Property(x => x.IsVerified).HasColumnName("verificada");
                // Stored as text so it stays readable in the database.
                e.Property(x => x.Status).HasColumnName("estado").HasConversion<string>().HasMaxLength(20);
                e.Property(x => x.RejectionReason).HasColumnName("motivo_rechazo").HasMaxLength(TextLimits.ReviewRejectionReason);
                e.Property(x => x.ResolvedAt).HasColumnName("fecha_resolucion").HasColumnType("timestamptz");
                e.Property(x => x.ResolvedByUserId).HasColumnName("resuelta_por_id");
                e.Property(x => x.PublishedAt).HasColumnName("fecha_publicacion").HasColumnType("timestamptz");
                e.Property(x => x.CreatedAt).HasColumnName("fecha_creacion").HasColumnType("timestamptz");
                e.Property(x => x.UpdatedAt).HasColumnName("fecha_actualizacion").HasColumnType("timestamptz");
                e.HasIndex(x => new { x.ZoneSlug, x.UserId }).IsUnique();
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne<User>().WithMany().HasForeignKey(x => x.ResolvedByUserId).OnDelete(DeleteBehavior.SetNull);
                e.HasMany(x => x.HelpfulVotes).WithOne().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ReviewHelpfulVote>(e =>
            {
                e.ToTable("resenas_votos_util");
                e.HasKey(x => new { x.ReviewId, x.UserId });
                e.Property(x => x.ReviewId).HasColumnName("resena_id");
                e.Property(x => x.UserId).HasColumnName("usuario_id");
                e.Property(x => x.CreatedAt).HasColumnName("fecha").HasColumnType("timestamptz");
                e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Question>(e =>
            {
                e.ToTable("preguntas");
                e.HasKey(x => x.Id);
                e.Property(x => x.ZoneSlug).HasColumnName("zona_slug").HasMaxLength(200);
                e.HasIndex(x => x.ZoneSlug);
                e.Property(x => x.UserId).HasColumnName("usuario_id");
                e.Property(x => x.Text).HasColumnName("texto").HasMaxLength(TextLimits.QuestionText);
                e.Property(x => x.CreatedAt).HasColumnName("fecha_creacion").HasColumnType("timestamptz");
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.Answers).WithOne().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Answer>(e =>
            {
                e.ToTable("respuestas");
                e.HasKey(x => x.Id);
                e.Property(x => x.QuestionId).HasColumnName("pregunta_id");
                e.Property(x => x.UserId).HasColumnName("usuario_id");
                e.Property(x => x.Text).HasColumnName("texto").HasMaxLength(TextLimits.AnswerText);
                e.Property(x => x.CreatedAt).HasColumnName("fecha_creacion").HasColumnType("timestamptz");
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Contribution>(e =>
            {
                e.ToTable("aportes");
                e.HasKey(x => x.Id);
                e.Property(x => x.ZoneSlug).HasColumnName("zona_slug").HasMaxLength(200);
                e.HasIndex(x => new { x.ZoneSlug, x.Status });
                // Point-of-interest queries look up approved corrections by place.
                e.HasIndex(x => new { x.PointOfInterestId, x.Status });
                // Store enums as text so they remain readable in the database and usable by map SQL queries.
                e.Property(x => x.Type).HasColumnName("tipo").HasConversion(new ContributionTypeValueConverter()).HasMaxLength(20);
                e.Property(x => x.Status).HasColumnName("estado").HasConversion(new ContributionStatusValueConverter()).HasMaxLength(20);
                e.Property(x => x.Reason).HasColumnName("motivo").HasConversion(new CorrectionReasonValueConverter()).HasMaxLength(20);
                e.Property(x => x.UserId).HasColumnName("usuario_id");
                e.Property(x => x.Name).HasColumnName("nombre").HasMaxLength(TextLimits.PlaceName);
                e.Property(x => x.Category).HasColumnName("categoria").HasMaxLength(40);
                e.Property(x => x.Location).HasColumnName("ubicacion").HasColumnType("geometry(Point,4326)");
                e.Property(x => x.OpeningHours).HasColumnName("horario").HasMaxLength(TextLimits.OpeningHours);
                e.Property(x => x.Address).HasColumnName("direccion").HasMaxLength(TextLimits.Address);
                e.Property(x => x.LivesOrWorksInZone).HasColumnName("vive_o_trabaja_en_zona");
                e.Property(x => x.PointOfInterestId).HasColumnName("punto_interes_id").HasMaxLength(40);
                e.Property(x => x.PointOfInterestName).HasColumnName("punto_interes_nombre").HasMaxLength(TextLimits.Address);
                e.Property(x => x.Comment).HasColumnName("comentario").HasMaxLength(TextLimits.Comment);
                e.Property(x => x.CreatedAt).HasColumnName("fecha_creacion").HasColumnType("timestamptz");
                e.Property(x => x.ResolvedAt).HasColumnName("fecha_resolucion").HasColumnType("timestamptz");
                e.Property(x => x.ResolvedByUserId).HasColumnName("resuelto_por_id");
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne<User>().WithMany().HasForeignKey(x => x.ResolvedByUserId).OnDelete(DeleteBehavior.SetNull);
                e.HasMany(x => x.Votes).WithOne().HasForeignKey(x => x.ContributionId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ContributionVote>(e =>
            {
                e.ToTable("aportes_validaciones");
                e.HasKey(x => new { x.ContributionId, x.UserId });
                e.Property(x => x.ContributionId).HasColumnName("aporte_id");
                e.Property(x => x.UserId).HasColumnName("usuario_id");
                e.Property(x => x.Confirms).HasColumnName("confirma");
                e.Property(x => x.CreatedAt).HasColumnName("fecha").HasColumnType("timestamptz");
                e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            // Approved contributions persisted as points of interest, read by the map queries over OSM
            modelBuilder.Entity<CommunityPoi>(e =>
            {
                e.ToTable("community_pois");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).HasMaxLength(40);
                e.Property(x => x.ZoneSlug).HasMaxLength(200);
                e.Property(x => x.Name).HasMaxLength(TextLimits.PlaceName);
                e.Property(x => x.Category).HasMaxLength(40);
                e.Property(x => x.Location).HasColumnType("geometry(Point,4326)");
                e.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
                e.HasIndex(x => x.Location).HasMethod("gist");
                e.HasOne<Contribution>().WithMany().HasForeignKey(x => x.LastContributionId).OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<GroupInvitation>()
                .Property(i => i.Status)
                .HasConversion<string>();

            modelBuilder.Entity<Participant>()
                .Property(p => p.Role)
                .HasConversion<string>();

            // A user takes part in a group only once.
            modelBuilder.Entity<Participant>()
                .HasIndex(p => new { p.GroupId, p.UserId })
                .IsUnique();
        }

        private static string ToSnakeCase(string name) =>
            string.Concat(name.Select((c, i) =>
                i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
    }
}
