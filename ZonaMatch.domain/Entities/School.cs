using NetTopologySuite.Geometries;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Domain.Entities;

// School record from the education registry (geo.escuelas table), loaded from the geographic data dump.
public class School : IPointLocation
{
    public string NaturalKey { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? CueAnnex { get; set; }
    public string? Cue { get; set; }
    public int? Annex { get; set; }
    public string? SchoolCode { get; set; }
    public string? SchoolNumber { get; set; }
    public string? Cui { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? OrganizationType { get; set; }

    // For example, "Nivel Inicial".
    public string? Level { get; set; }
    public int? LevelId { get; set; }

    // For example, "Estatal" or "Privado".
    public string? Sector { get; set; }
    public int? SectorId { get; set; }
    public string? ManagingAuthority { get; set; }
    public int? ManagingAuthorityId { get; set; }
    public string? Modality { get; set; }
    public int? ModalityId { get; set; }
    public int? OrganizationTypeId { get; set; }

    public string? DistrictName { get; set; }
    public string? DistrictId { get; set; }

    // Logical reference to District.Key; the dump has no foreign key.
    public string? DistrictKey { get; set; }
    public string? Locality { get; set; }
    public string? LocalityId { get; set; }
    public string? ElectoralSection { get; set; }
    public string? Region { get; set; }
    public string? EducationalRegion { get; set; }

    public string? Street { get; set; }
    public string? Number { get; set; }
    public string? RightSideStreet { get; set; }
    public string? LeftSideStreet { get; set; }
    public string? PostalCode { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Rpv { get; set; }
    public string? SchoolSite { get; set; }
    public string? SiteYearExtension { get; set; }
    public string? Area { get; set; }
    public string? Scope { get; set; }
    public string? DisadvantageLevel { get; set; }
    public string? Subsidy { get; set; }
    public string? Category { get; set; }
    public string? Shifts { get; set; }
    public int? Sections { get; set; }
    public int? Enrollment { get; set; }
    public int? MaleStudents { get; set; }
    public int? FemaleStudents { get; set; }
    public string? Period { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // Database column "geom": geometry(Point,4326).
    public Point Location { get; set; } = null!;
}
