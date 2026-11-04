using ZonaMatch.Application.Exceptions;

namespace ZonaMatch.Application.Policies;

public static class GeolocationRequestValidator
{
    public const double MaximumPointOfInterestRadiusMeters = 5_000;

    public static void ValidateCoordinates(double latitude, double longitude, double radiusMeters = 0)
    {
        if (latitude is < -90 or > 90)
            throw new BadRequestException("La latitud debe estar entre -90 y 90.");

        if (longitude is < -180 or > 180)
            throw new BadRequestException("La longitud debe estar entre -180 y 180.");

        if (radiusMeters < 0)
            throw new BadRequestException("El radio no puede ser negativo.");
    }

    public static void ValidatePointOfInterestRadius(double radiusMeters)
    {
        if (radiusMeters > MaximumPointOfInterestRadiusMeters)
            throw new BadRequestException($"El radio no puede superar los {MaximumPointOfInterestRadiusMeters} metros.");
    }

    public static void ValidateQuantity(int quantity)
    {
        if (quantity <= 0)
            throw new BadRequestException("La cantidad debe ser mayor a 0.");
    }
}
