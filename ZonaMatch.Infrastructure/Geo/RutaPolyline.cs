namespace ZonaMatch.Infrastructure.Geo;

// Longitud de una geometria de MOTIS: suma todos los segmentos, respetando su precision declarada.
public static class RutaPolyline
{
    public static double? LongitudMetros(string? puntos, int? precision)
    {
        if (string.IsNullOrEmpty(puntos)
            || precision is not { } decimales
            || decimales is < 0 or > 7)
            return null;

        var factor = Math.Pow(10, decimales);
        var indice = 0;
        var cantidad = 0;
        var latitud = 0.0;
        var longitud = 0.0;
        var metros = 0.0;
        (double Latitud, double Longitud)? anterior = null;

        while (indice < puntos.Length)
        {
            if (!LeerDelta(puntos, ref indice, out var deltaLatitud)
                || !LeerDelta(puntos, ref indice, out var deltaLongitud))
                return null;

            latitud += deltaLatitud / factor;
            longitud += deltaLongitud / factor;

            if (latitud is < -90 or > 90 || longitud is < -180 or > 180)
                return null;

            if (anterior is { } previo)
                metros += GeoMath.DistanciaMetros(previo.Latitud, previo.Longitud, latitud, longitud);

            anterior = (latitud, longitud);
            cantidad++;
        }

        return cantidad >= 2 ? metros : null;
    }

    private static bool LeerDelta(string puntos, ref int indice, out long delta)
    {
        delta = 0;
        long valor = 0;
        var desplazamiento = 0;

        while (indice < puntos.Length && desplazamiento <= 55)
        {
            var parte = puntos[indice++] - 63;
            if (parte is < 0 or > 63)
                return false;

            valor |= (long)(parte & 31) << desplazamiento;
            if (parte < 32)
            {
                delta = (valor & 1) != 0 ? ~(valor >> 1) : valor >> 1;
                return true;
            }

            desplazamiento += 5;
        }

        return false;
    }
}
