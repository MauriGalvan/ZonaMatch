using FluentAssertions;
using Moq;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models.Geolocation;
using ZonaMatch.Application.Services;
using ZonaMatch.Application.UseCases.Geolocation;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Geolocation;

public class ReverseGeocodeTests
{
    [Fact]
    public async Task ReverseGeocode_WhenProviderReturnsAddress_EnrichesItWithLocalBoundaries()
    {
        var geocoder = new Mock<IGeocodingClient>();
        geocoder.Setup(client => client.ReverseAsync(-34.6, -58.4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeocodedAddress("Work", "Main Street", "123", "Locality", "Buenos Aires", "1000", "Main Street 123"));
        var districts = new Mock<IDistrictRepository>();
        districts.Setup(repository => repository.GetContainingPointAsync(-34.6, -58.4))
            .ReturnsAsync(new District { Key = "district-1", Name = "District One", Province = "Buenos Aires" });
        var communes = new Mock<ICommuneRepository>();
        communes.Setup(repository => repository.GetContainingPointAsync(-34.6, -58.4))
            .ReturnsAsync(new Commune { Key = "commune-1", Name = "Commune One" });
        var useCase = new ReverseGeocode(
            geocoder.Object, new LocationBoundaryLookup(districts.Object, communes.Object));

        var result = await useCase.ExecuteAsync(-34.6, -58.4);

        result.Name.Should().Be("Work");
        result.District.Should().Be("District One");
        result.Commune.Should().Be("Commune One");
        result.Province.Should().Be("Buenos Aires");
    }

    [Fact]
    public async Task ReverseGeocode_WithProvider_UsesTheClientTheSelectorReturns()
    {
        var defaultGeocoder = new Mock<IGeocodingClient>();
        var photonGeocoder = new Mock<IGeocodingClient>();
        photonGeocoder.Setup(client => client.ReverseAsync(-34.6, -58.4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeocodedAddress(null, "Gutenberg", "666", "Villa Luzuriaga", null, null, "Gutenberg 666, Villa Luzuriaga"));
        var selector = new Mock<IGeocodingClientSelector>();
        selector.Setup(s => s.Resolve("photon")).Returns(photonGeocoder.Object);
        var districts = new Mock<IDistrictRepository>();
        var communes = new Mock<ICommuneRepository>();
        var useCase = new ReverseGeocode(
            defaultGeocoder.Object, new LocationBoundaryLookup(districts.Object, communes.Object), selector.Object);

        var result = await useCase.ExecuteAsync(-34.6, -58.4, "photon");

        result.StreetNumber.Should().Be("666");
        defaultGeocoder.Verify(
            client => client.ReverseAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReverseGeocode_WhenProviderAndBoundariesHaveNoResult_ThrowsNotFound()
    {
        var geocoder = new Mock<IGeocodingClient>();
        geocoder.Setup(client => client.ReverseAsync(-34.6, -58.4, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodedAddress?)null);
        var districts = new Mock<IDistrictRepository>();
        districts.Setup(repository => repository.GetContainingPointAsync(-34.6, -58.4)).ReturnsAsync((District?)null);
        var communes = new Mock<ICommuneRepository>();
        communes.Setup(repository => repository.GetContainingPointAsync(-34.6, -58.4)).ReturnsAsync((Commune?)null);
        var useCase = new ReverseGeocode(
            geocoder.Object, new LocationBoundaryLookup(districts.Object, communes.Object));

        Func<Task> action = () => useCase.ExecuteAsync(-34.6, -58.4);

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ReverseGeocode_WhenProviderIsUnavailable_ThrowsBadGatewayException()
    {
        var geocoder = new Mock<IGeocodingClient>();
        geocoder.Setup(client => client.ReverseAsync(-34.6, -58.4, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Geocoder unavailable"));
        var useCase = new ReverseGeocode(
            geocoder.Object,
            new LocationBoundaryLookup(new Mock<IDistrictRepository>().Object, new Mock<ICommuneRepository>().Object));

        Func<Task> action = () => useCase.ExecuteAsync(-34.6, -58.4);

        await action.Should().ThrowAsync<BadGatewayException>();
    }
}
