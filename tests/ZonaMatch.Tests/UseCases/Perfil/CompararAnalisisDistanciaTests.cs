using Moq;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.AnalisisGuardados;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Puntaje;

namespace ZonaMatch.Tests.UseCases.Perfil;

public class CompararAnalisisDistanciaTests
{
    [Fact]
    public async Task Comparar_conserva_la_distancia_en_los_lugares_de_cada_analisis_personal()
    {
        var usuarioId = Guid.NewGuid();
        var analisis = Enumerable.Range(1, 2).Select(numero => new Analisis
        {
            UsuarioId = usuarioId,
            Nombre = $"Busqueda {numero}",
            Zonas = [new AnalisisZona { ZonaSlug = "recoleta", Nombre = "Recoleta", Orden = 1 }],
            Contexto = new Contexto
            {
                UsuarioId = usuarioId,
                Movilidad = ["vehiculo"],
                Puntos = [new PuntoDeInteres { Nombre = "Trabajo", Latitude = -34.6, Longitude = -58.4, TiempoMax = "45 min" }]
            }
        }).ToArray();

        var repositorio = new Mock<IAnalisisRepository>();
        repositorio.Setup(r => r.ListarDeUsuarioAsync(usuarioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(analisis);
        var perfiles = new Mock<IPerfilesZonaRepository>();
        perfiles.Setup(r => r.ListarAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PerfilZona[] { new() { Slug = "recoleta", Nombre = "Recoleta", Nivel = 9 } });
        var ruteo = new Mock<IRuteoClient>();
        ruteo.Setup(r => r.CalcularAsync(
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
                ModoViaje.Auto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TiempoViaje(25, false, 8.75));

        var resultado = await new CompararAnalisis(repositorio.Object, perfiles.Object, ruteo.Object)
            .EjecutarAsync(usuarioId, new CompararAnalisisRequest { AnalisisIds = analisis.Select(a => a.Id).ToList() });

        Assert.Equal(2, resultado.Analisis.Count);
        Assert.All(resultado.Analisis, comparado =>
        {
            var lugar = Assert.Single(Assert.Single(comparado.Zonas).Lugares);
            Assert.Equal(25, lugar.Minutos);
            Assert.Equal(8.75, lugar.DistanciaKm!.Value, 3);
        });
    }
}
