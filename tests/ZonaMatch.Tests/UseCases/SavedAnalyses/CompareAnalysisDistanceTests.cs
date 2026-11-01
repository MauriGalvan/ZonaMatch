using Moq;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.SavedAnalyses;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Tests.UseCases.SavedAnalyses;

public class CompareAnalysisDistanceTests
{
    [Fact]
    public async Task Compare_PreservesDistanceForPlacesInEachPersonalAnalysis()
    {
        var userId = Guid.NewGuid();
        var analyses = Enumerable.Range(1, 2).Select(number => new Analisis
        {
            UsuarioId = userId,
            Nombre = $"Busqueda {number}",
            Zonas = [new AnalisisZona { ZonaSlug = "recoleta", Nombre = "Recoleta", Orden = 1 }],
            Contexto = new Contexto
            {
                UsuarioId = userId,
                Movilidad = ["vehiculo"],
                Puntos = [new PuntoDeInteres { Nombre = "Trabajo", Latitude = -34.6, Longitude = -58.4, TiempoMax = "45 min" }]
            }
        }).ToArray();

        var repositorio = new Mock<IAnalisisRepository>();
        repositorio.Setup(r => r.ListarDeUsuarioAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(analyses);
        var perfiles = new Mock<IPerfilesZonaRepository>();
        perfiles.Setup(r => r.ListarAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ZoneProfile[] { new() { Slug = "recoleta", Nombre = "Recoleta", Nivel = 9 } });
        var ruteo = new Mock<IRuteoClient>();
        ruteo.Setup(r => r.CalcularAsync(
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
                TravelMode.Car, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelTime(25, false, 8.75));

        var result = await new CompareAnalyses(repositorio.Object, perfiles.Object, ruteo.Object)
            .ExecuteAsync(userId, new CompareAnalysesRequest { AnalysisIds = analyses.Select(analysis => analysis.Id).ToList() });

        Assert.Equal(2, result.Analyses.Count);
        Assert.All(result.Analyses, comparedAnalysis =>
        {
            var place = Assert.Single(Assert.Single(comparedAnalysis.Zones).Places);
            Assert.Equal(25, place.Minutes);
            Assert.Equal(8.75, place.DistanceKm!.Value, 3);
        });
    }
}
