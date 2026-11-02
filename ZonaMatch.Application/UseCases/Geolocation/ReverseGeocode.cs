using ZonaMatch.Application.DTOs.Geolocation.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models.Geolocation;
using ZonaMatch.Application.Policies;
using ZonaMatch.Application.Services;

namespace ZonaMatch.Application.UseCases.Geolocation;

public sealed class ReverseGeocode(
    IGeocodingClient geocodingClient,
    LocationBoundaryLookup boundaryLookup)
{
    public async Task<ReverseGeocodeResponse> ExecuteAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        GeolocationRequestValidator.ValidateCoordinates(latitude, longitude);

        GeocodedAddress? geocodedAddress;
        try
        {
            geocodedAddress = await geocodingClient.ReverseAsync(latitude, longitude, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new BadGatewayException("El servicio de geocodificacion no esta disponible.", exception);
        }

        // The repositories share a scoped DbContext, so keep these queries sequential.
        var district = await boundaryLookup.GetDistrictAsync(latitude, longitude);
        var commune = await boundaryLookup.GetCommuneAsync(latitude, longitude);

        if (geocodedAddress is null && district is null && commune is null)
            throw new NotFoundException("No se encontro una direccion para esa ubicacion.");

        return new ReverseGeocodeResponse(
            Location: new CoordinateResponse(latitude, longitude),
            Name: geocodedAddress?.Name,
            Street: geocodedAddress?.Street,
            StreetNumber: geocodedAddress?.StreetNumber,
            Locality: geocodedAddress?.Locality,
            District: district?.Name,
            Commune: commune?.Name,
            Province: district?.Provincia ?? geocodedAddress?.Province,
            PostalCode: geocodedAddress?.PostalCode,
            FullAddress: geocodedAddress?.FullAddress);
    }
}
