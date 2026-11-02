using Moq;
using ZonaMatch.Application.DTOs.Analysis.Requests;
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
        var analyses = Enumerable.Range(1, 2).Select(number => new Analysis
        {
            UsuarioId = userId,
            Nombre = $"Busqueda {number}",
            Zonas = [new AnalysisZone { ZonaSlug = "recoleta", Nombre = "Recoleta", Orden = 1 }],
            Context = new Context
            {
                UsuarioId = userId,
                Movilidad = ["vehiculo"],
                Puntos = [new ContextPlace { Nombre = "Trabajo", Latitude = -34.6, Longitude = -58.4, TiempoMax = "45 min" }]
            }
        }).ToArray();

        var repositorio = new Mock<IAnalysisRepository>();
        repositorio.Setup(r => r.GetForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(analyses);
        var perfiles = new Mock<IZoneProfilesRepository>();
        perfiles.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ZoneProfile[] { new() { Slug = "recoleta", Nombre = "Recoleta", Nivel = 9 } });
        var ruteo = new Mock<IRoutingClient>();
        ruteo.Setup(r => r.GetTravelTimeAsync(
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
