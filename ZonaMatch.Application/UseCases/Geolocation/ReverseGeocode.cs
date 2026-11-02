using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;
using ZonaMatch.Application.Services;

namespace ZonaMatch.Application.UseCases.Geolocation;

public sealed class ReverseGeocode(
    IGeocodingClient geocodingClient,
    LocationBoundaryLookup boundaryLookup)
{
    public async Task<DireccionDto> ExecuteAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        GeolocationRequestValidator.ValidateCoordinates(latitude, longitude);

        DireccionGeocodificadaDto? geocodedAddress;
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

        return new DireccionDto(
            Ubicacion: new CoordenadaDto(latitude, longitude),
            Nombre: geocodedAddress?.Nombre,
            Calle: geocodedAddress?.Calle,
            Altura: geocodedAddress?.Altura,
            Localidad: geocodedAddress?.Localidad,
            District: district?.Nombre,
            Commune: commune?.Nombre,
            Provincia: district?.Provincia ?? geocodedAddress?.Provincia,
            CodigoPostal: geocodedAddress?.CodigoPostal,
            DireccionCompleta: geocodedAddress?.DireccionCompleta);
    }
}
