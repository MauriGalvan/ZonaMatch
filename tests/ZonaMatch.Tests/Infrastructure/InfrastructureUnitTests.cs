using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ZonaMatch.Api.Infrastructure;
using ZonaMatch.Application.Ingestion;
using ZonaMatch.Domain.Scoring;
using ZonaMatch.Infrastructure;
using ZonaMatch.Infrastructure.Data;
using ZonaMatch.Infrastructure.Routing;
using ZonaMatch.Infrastructure.Sources;
using ZonaMatch.Tests.Support;

namespace ZonaMatch.Tests.Infrastructure
{
    public class StoreNamingTests
    {
        [Theory]
        [InlineData("ZoneId", "zone_id")]
        [InlineData("PK_zones", "pk_zones")]
        [InlineData("IX_points_of_interest_location", "ix_points_of_interest_location")]
        [InlineData("AreaM2", "area_m2")]
        [InlineData("Area2Km", "area2_km")]
        [InlineData("HTMLParser", "html_parser")]
        [InlineData("id", "id")]
        [InlineData("ParentIdX", "parent_id_x")]
        public void ToSnakeCase(string input, string expected)
        {
            Assert.Equal(expected, StoreNaming.ToSnakeCase(input));
        }
    }

    public class SqlIdentifierTests
    {
        [Theory]
        [InlineData("geom", "\"geom\"")]
        [InlineData("zm.radios", "\"zm\".\"radios\"")]
        [InlineData("addr:housenumber", "\"addr:housenumber\"")]
        public void Quote_cita_identificadores_validos(string input, string expected)
        {
            Assert.Equal(expected, SqlIdentifier.Quote(input));
        }

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("a.b.c")]
        [InlineData("radios; DROP TABLE x")]
        [InlineData("1tabla")]
        [InlineData("zm.\"radios\"")]
        public void Quote_rechaza_identificadores_peligrosos(string input)
        {
            Assert.Throws<ArgumentException>(() => SqlIdentifier.Quote(input));
        }
    }

    public class SourceReaderQueryTests
    {
        [Fact]
        public void Consulta_territorial_minima()
        {
            var sql = PostgisTerritorialSourceReader.BuildQuery(new TerritorialSourceOptions
            {
                Table = "zm.comunas",
                IdColumn = "key",
                NameColumn = "nombre",
                GeometryColumn = "geom",
            });

            Assert.Contains("FROM \"zm\".\"comunas\" t", sql);
            Assert.Contains("NULL::text AS code", sql);
            Assert.Contains("NULL::text AS parent_code", sql);
            Assert.Contains("ORDER BY ST_Area(t.\"geom\") DESC))[1] AS name", sql);
            Assert.Contains("AND (TRUE)", sql);
            Assert.Contains("GROUP BY t.\"key\"", sql);
        }

        [Fact]
        public void Consulta_territorial_completa()
        {
            var sql = PostgisTerritorialSourceReader.BuildQuery(new TerritorialSourceOptions
            {
                Table = "zm.partidos",
                IdColumn = "censo_codigo",
                NameColumn = "nombre",
                CodeColumn = "censo_codigo",
                ParentCodeColumn = "provincia",
                GeometryColumn = "geom",
                Filter = "provincia = 'BUENOS AIRES'",
                PreferredRowCondition = "key NOT LIKE 'ISLAS %'",
            });

            Assert.Contains("CAST(t.\"censo_codigo\" AS text) ORDER BY (key NOT LIKE 'ISLAS %') DESC, ST_Area(t.\"geom\") DESC))[1] AS code", sql);
            Assert.Contains("CAST(t.\"provincia\" AS text)", sql);
            Assert.Contains("AND (provincia = 'BUENOS AIRES')", sql);
        }

        [Fact]
        public void Consulta_de_pois_con_y_sin_etiquetas()
        {
            var withTags = PostgisPoiSourceReader.BuildQuery(new PoiSourceOptions
            {
                Table = "public.planet_osm_point",
                IdColumn = "osm_id",
                NameColumn = "name",
                GeometryColumn = "way",
                AttributeColumns = ["amenity", "addr:housenumber"],
                TagsColumn = "tags",
                Filter = "amenity IS NOT NULL",
            });
            var withoutTags = PostgisPoiSourceReader.BuildQuery(new PoiSourceOptions
            {
                Table = "zm.escuelas",
                IdColumn = "clave_natural",
                NameColumn = "nombre",
                GeometryColumn = "geom",
            });

            Assert.Contains("hstore_to_jsonb(t.\"tags\")", withTags);
            Assert.Contains("AS a0", withTags);
            Assert.Contains("CAST(t.\"addr:housenumber\" AS text)))[1] AS a1", withTags);
            Assert.Contains("AND (amenity IS NOT NULL)", withTags);
            Assert.Contains("NULL::text AS tags", withoutTags);
            Assert.DoesNotContain("AS a0", withoutTags);
            Assert.Contains("AND (TRUE)", withoutTags);
        }

        [Fact]
        public void MergeAttributes_prioriza_columnas_y_descarta_vacios()
        {
            var attributes = PostgisPoiSourceReader.MergeAttributes(
                ["amenity", "shop", "name"],
                [" school ", null, "  "],
                """{"amenity":"college","addr:street":"Rivadavia","operator:type":"","note":null}""");

            Assert.Equal(new Dictionary<string, string> { ["amenity"] = "school", ["addr:street"] = "Rivadavia" }, attributes);
            Assert.Empty(PostgisPoiSourceReader.MergeAttributes([], [], null));
        }

        [Fact]
        public void BuildAddress_une_las_partes_disponibles()
        {
            var attributes = new Dictionary<string, string> { ["addr:street"] = "Rivadavia", ["addr:housenumber"] = "9800" };

            Assert.Equal("Rivadavia 9800", PostgisPoiSourceReader.BuildAddress(["addr:street", "addr:housenumber"], attributes));
            Assert.Equal("Rivadavia", PostgisPoiSourceReader.BuildAddress(["addr:street", "addr:unit"], attributes));
            Assert.Null(PostgisPoiSourceReader.BuildAddress(["direccion"], attributes));
        }
    }

    public class EstimatedRoutingServiceTests
    {
        [Fact]
        public async Task Estima_por_distancia_y_modo()
        {
            var estimate = await new EstimatedRoutingService().EstimateAsync(
                TestGeometry.Point(0, 0), TestGeometry.Point(0, 0.0449661), TravelMode.Walk, CancellationToken.None);

            Assert.Equal(EstimatedRoutingService.Method, estimate.Method);
            Assert.Equal(86.7d, estimate.Minutes);
        }
    }

    public class DependencyInjectionTests
    {
        [Fact]
        public void Exige_la_cadena_de_conexion()
        {
            var configuration = new ConfigurationBuilder().Build();

            var exception = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddInfrastructure(configuration));
            Assert.Contains(DependencyInjection.ConnectionStringName, exception.Message);
        }
    }

    public class ApiExceptionHandlerTests
    {
        [Fact]
        public async Task No_maneja_errores_inesperados()
        {
            var problemDetails = Substitute.For<IProblemDetailsService>();
            var handler = new ApiExceptionHandler(problemDetails);

            var handled = await handler.TryHandleAsync(new DefaultHttpContext(), new InvalidOperationException("boom"), CancellationToken.None);

            Assert.False(handled);
            await problemDetails.DidNotReceiveWithAnyArgs().TryWriteAsync(default!);
        }
    }
}
