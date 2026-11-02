using ZonaMatch.Application.DTOs.Geolocation.Responses;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Mapping;

public static class SchoolResponseMapper
{
    public static SchoolResponse Map(School school, double distanceMeters) =>
        new(school.ClaveNatural, school.Nombre, school.Nivel, school.Sector, school.Direccion, school.Localidad,
            school.Ubicacion.Y, school.Ubicacion.X, distanceMeters);
}
