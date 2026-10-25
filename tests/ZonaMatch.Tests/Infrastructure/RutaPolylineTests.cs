using ZonaMatch.Infrastructure.Geo;

namespace ZonaMatch.Tests.Infrastructure;

public class RutaPolylineTests
{
    [Fact]
    public void Mide_todos_los_segmentos_del_recorrido()
    {
        // (0,0) -> (1,0) -> (1,1): medir solo origen y destino daria unos 157 km.
        var metros = RutaPolyline.LongitudMetros("??_ibE??_ibE", 5);

        Assert.NotNull(metros);
        Assert.InRange(metros.Value, 222_000, 222_500);
    }

    [Theory]
    [InlineData(5, 111_194.9266)]
    [InlineData(6, 11_119.4927)]
    [InlineData(7, 1_111.9493)]
    public void Respeta_la_precision_indicada_por_Motis(int precision, double esperado)
    {
        var metros = RutaPolyline.LongitudMetros("??_ibE?", precision);

        Assert.NotNull(metros);
        Assert.Equal(esperado, metros.Value, 3);
    }

    [Theory]
    [InlineData(null, 5)]
    [InlineData("", 5)]
    [InlineData("_", 5)]
    [InlineData("??", 5)]
    [InlineData("??_ibE?", null)]
    [InlineData("??_ibE?", 8)]
    [InlineData("!", 5)]
    [InlineData("____________________", 5)]
    public void Geometria_incompleta_o_invalida_no_inventa_distancia(string? puntos, int? precision)
    {
        Assert.Null(RutaPolyline.LongitudMetros(puntos, precision));
    }

    [Fact]
    public void Puntos_repetidos_tienen_distancia_cero()
    {
        Assert.Equal(0.0, RutaPolyline.LongitudMetros("????", 5));
    }
}
