using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;

namespace ZonaMatch.Domain.Geo
{
    public static class SpatialReference
    {
        public const int Wgs84 = 4326;
        public const int WebMercator = 3857;
    }

    // lleva cualquier geometría de origen al formato canónico: EPSG:4326 y tipos fijos
    public static class GeometryNormalizer
    {
        private const double EarthRadiusMeters = 6378137d;

        public static readonly GeometryFactory Factory = new(new PrecisionModel(), SpatialReference.Wgs84);

        public static Geometry ToWgs84(Geometry geometry)
        {
            ArgumentNullException.ThrowIfNull(geometry);

            var copy = geometry.Copy();

            switch (geometry.SRID)
            {
                case SpatialReference.Wgs84:
                    break;
                case SpatialReference.WebMercator:
                    copy.Apply(new WebMercatorToWgs84Filter());
                    copy.GeometryChanged();
                    break;
                default:
                    throw new NotSupportedException($"SRID {geometry.SRID} no soportado. Se esperan {SpatialReference.Wgs84} o {SpatialReference.WebMercator}.");
            }

            copy.SRID = SpatialReference.Wgs84;
            return copy;
        }

        // unidades territoriales: siempre MultiPolygon válido
        public static MultiPolygon ToMultiPolygon(Geometry geometry)
        {
            var wgs84 = ToWgs84(geometry);
            var valid = wgs84.IsValid ? wgs84 : GeometryFixer.Fix(wgs84);

            // aplana colecciones anidadas (ST_Collect de varios MultiPolygon devuelve una GeometryCollection)
            var polygons = PolygonExtracter.GetPolygons(valid)
                .Cast<Polygon>()
                .Where(polygon => !polygon.IsEmpty)
                .ToArray();

            if (polygons.Length == 0)
            {
                throw new ArgumentException($"La geometría {geometry.GeometryType} no contiene polígonos.", nameof(geometry));
            }

            var multiPolygon = Factory.CreateMultiPolygon(polygons);

            if (multiPolygon.IsValid)
            {
                return multiPolygon;
            }

            // partes superpuestas: se disuelven en una sola superficie
            var dissolved = PolygonExtracter.GetPolygons(multiPolygon.Union()).Cast<Polygon>().ToArray();
            return Factory.CreateMultiPolygon(dissolved);
        }

        // POIs: punto representativo, dentro del polígono si la fuente trae una superficie
        public static Point ToRepresentativePoint(Geometry geometry)
        {
            var wgs84 = ToWgs84(geometry);

            if (wgs84.IsEmpty)
            {
                throw new ArgumentException("La geometría está vacía.", nameof(geometry));
            }

            var point = wgs84 as Point ?? (Point)wgs84.InteriorPoint;
            return Factory.CreatePoint(point.Coordinate);
        }

        private sealed class WebMercatorToWgs84Filter : IEntireCoordinateSequenceFilter
        {
            public bool Done => false;
            public bool GeometryChanged => true;

            public void Filter(CoordinateSequence seq)
            {
                for (var index = 0; index < seq.Count; index++)
                {
                    var x = seq.GetX(index);
                    var y = seq.GetY(index);
                    seq.SetX(index, x / EarthRadiusMeters * 180d / Math.PI);
                    seq.SetY(index, (2d * Math.Atan(Math.Exp(y / EarthRadiusMeters)) - Math.PI / 2d) * 180d / Math.PI);
                }
            }
        }
    }
}
