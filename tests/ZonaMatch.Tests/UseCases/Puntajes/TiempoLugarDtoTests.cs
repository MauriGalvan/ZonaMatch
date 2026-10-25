using System.Text.Json;
using ZonaMatch.Application.DTOs;

namespace ZonaMatch.Tests.UseCases.Puntajes;

public class TiempoLugarDtoTests
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(8.75)]
    [InlineData(null)]
    public void Serializa_distancia_como_numero_o_null_con_el_contrato_web(double? distancia)
    {
        var lugar = new TiempoLugarDto("Trabajo", 25, 45, false, distancia);
        var json = JsonSerializer.Serialize(lugar, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var documento = JsonDocument.Parse(json);
        var campo = documento.RootElement.GetProperty("distanciaKm");

        if (distancia is { } km)
            Assert.Equal(km, campo.GetDouble());
        else
            Assert.Equal(JsonValueKind.Null, campo.ValueKind);
    }
}
