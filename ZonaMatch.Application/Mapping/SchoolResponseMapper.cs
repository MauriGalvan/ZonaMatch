using ZonaMatch.Application.DTOs.Geolocation.Responses;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Mapping;

public static class SchoolResponseMapper
{
    public static SchoolResponse Map(School school, double distanceMeters) =>
        new(school.NaturalKey, school.Name, school.Level, school.Sector, school.Address, school.Locality,
            school.Location.Y, school.Location.X, distanceMeters);
}
