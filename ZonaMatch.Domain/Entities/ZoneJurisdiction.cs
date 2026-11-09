namespace ZonaMatch.Domain.Entities
{
    // An administrative boundary that contains a zone (its commune, district, or CABA).
    public sealed record ZoneJurisdiction(string Name, int AdministrativeLevel);
}
