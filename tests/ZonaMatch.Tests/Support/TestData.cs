using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using ZonaMatch.Application.Ingestion;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Territory;

namespace ZonaMatch.Tests.Support
{
    public static class TestData
    {
        public static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

        // las entidades reciben el id desde la base; en pruebas unitarias se asigna por reflexión
        public static T WithId<T>(T entity, object id)
        {
            typeof(T).GetProperty("Id")!.SetValue(entity, id);
            return entity;
        }

        public static async IAsyncEnumerable<T> ToAsync<T>(params T[] items)
        {
            foreach (var item in items)
            {
                await Task.Yield();
                yield return item;
            }
        }

        public static IOptions<IngestionOptions> Options(Action<IngestionOptions>? configure = null)
        {
            var options = new IngestionOptions();
            configure?.Invoke(options);
            return Microsoft.Extensions.Options.Options.Create(options);
        }

        public static TimeProvider Clock()
        {
            var clock = NSubstitute.Substitute.For<TimeProvider>();
            NSubstitute.SubstituteExtensions.Returns(clock.GetUtcNow(), Now);
            return clock;
        }

        public static ZoneData Zone(
            int id,
            string slug,
            double longitude,
            long unitId,
            long? parentUnitId = null,
            string name = "Zona",
            string parentName = "La Matanza",
            string? parentUnitName = "La Matanza",
            TerritorialUnitType type = TerritorialUnitType.Localidad)
        {
            // el centro queda exactamente en la longitud indicada para poder razonar los cálculos
            var square = TestGeometry.MultiSquare(longitude - 0.005, -34.665, 0.01);
            return new ZoneData(id, slug, name, parentName, type, unitId, parentUnitId, parentUnitName,
                1_234_567d, TestGeometry.Point(longitude, -34.66), square);
        }
    }
}
