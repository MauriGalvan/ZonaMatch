using ZonaMatch.Application.DTOs.Geolocation.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models.Geolocation;
using ZonaMatch.Application.Policies;
using ZonaMatch.Application.Services;

namespace ZonaMatch.Application.UseCases.Geolocation;

public sealed class ReverseGeocode(
    IGeocodingClient geocodingClient,
    LocationBoundaryLookup boundaryLookup,
    IGeocodingClientSelector? clientSelector = null)
{
    public Task<ReverseGeocodeResponse> ExecuteAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(latitude, longitude, null, cancellationToken);

    // provider: "osm", "nominatim" or "photon" to compare geocoders; null uses the configured default.
    public async Task<ReverseGeocodeResponse> ExecuteAsync(
        double latitude,
        double longitude,
        string? provider,
        CancellationToken cancellationToken = default)
    {
        GeolocationRequestValidator.ValidateCoordinates(latitude, longitude);

        var client = provider is null || clientSelector is null
            ? geocodingClient
            : clientSelector.Resolve(provider);

        GeocodedAddress? geocodedAddress;
        try
        {
            geocodedAddress = await client.ReverseAsync(latitude, longitude, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new BadGatewayException("El servicio de geocodificación no está disponible.", exception);
        }

        // The repositories share a scoped DbContext, so keep these queries sequential.
        var district = await boundaryLookup.GetDistrictAsync(latitude, longitude);
        var commune = await boundaryLookup.GetCommuneAsync(latitude, longitude);

        if (geocodedAddress is null && district is null && commune is null)
            throw new NotFoundException("No se encontró una dirección para esa ubicación.");

        return new ReverseGeocodeResponse(
            Location: new CoordinateResponse(latitude, longitude),
            Name: geocodedAddress?.Name,
            Street: geocodedAddress?.Street,
            StreetNumber: geocodedAddress?.StreetNumber,
            Locality: geocodedAddress?.Locality,
            District: district?.Name,
            Commune: commune?.Name,
            Province: district?.Province ?? geocodedAddress?.Province,
            PostalCode: geocodedAddress?.PostalCode,
            FullAddress: geocodedAddress?.FullAddress);
    }
}
