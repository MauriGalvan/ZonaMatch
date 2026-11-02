using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Scoring;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Services;

// Shared validation and mapping for profile updates and saved analysis versions.
public static class AnalysisCriteriaBuilder
{
    public const int MinimumPeople = 1;
    public const int MaximumPeople = 8;

    private static readonly HashSet<string> ValidPriorities = new(StringComparer.Ordinal)
    {
        "baja", "media", "alta",
    };

    // Validate the complete request before mutating the context so invalid input cannot partially update it.
    public static void Apply(Context context, AnalysisCriteriaRequest request, IReadOnlyList<Criterion> catalog)
    {
        var householdMembers = BuildHouseholdMembers(request.Context);
        var criteria = BuildCriteria(request.Criteria, catalog);
        var places = BuildPlaces(request.Points);
        var (operation, purchaseLimit, propertyType) = HousingScoring.Validate(
            request.Points.Operation, request.Points.PurchaseLimitUsd, request.Points.PropertyType);
        var areaM2 = HousingScoring.ValidateSurface(request.Points.AreaM2);

        context.PeopleCount = request.Context.PeopleCount;
        context.Pets = request.Context.Pets.Distinct(StringComparer.Ordinal).ToArray();
        context.Mobility = request.Context.Mobility.Distinct(StringComparer.Ordinal).ToArray();
        context.MaximumRent = request.Points.RentLimit;
        context.Operation = operation;
        context.MaximumPurchasePriceUsd = purchaseLimit;
        context.HousingType = propertyType;
        context.AreaM2 = areaM2;

        Replace(context.People, householdMembers);
        Replace(context.Criteria, criteria);
        Replace(context.Places, places);
    }

    private static List<Person> BuildHouseholdMembers(AnalysisContextRequest? request)
    {
        if (request is null)
            throw new BadRequestException("El contexto es obligatorio.");

        if (request.PeopleCount < MinimumPeople || request.PeopleCount > MaximumPeople)
            throw new BadRequestException($"La cantidad de personas debe estar entre {MinimumPeople} y {MaximumPeople}.");

        if (request.AgeRanges.Count > request.PeopleCount)
            throw new BadRequestException("Hay más edades que personas en el contexto.");

        if (request.PersonNames is null)
            throw new BadRequestException("Los nombres de las personas deben ser una lista.");

        if (request.PersonNames.Count > request.PeopleCount)
            throw new BadRequestException("Hay más nombres que personas en el contexto.");

        if (request.PersonNames.Count > 0 && request.PersonNames.Count != request.PeopleCount)
            throw new BadRequestException("Debe indicarse un nombre para cada persona del contexto.");

        if (request.PersonNames.Any(name => string.IsNullOrWhiteSpace(name)))
            throw new BadRequestException("El nombre de cada persona es obligatorio.");

        if (request.PersonNames.Any(name => name!.Trim().Length > 50))
            throw new BadRequestException("El nombre de una persona no puede superar 50 caracteres.");

        if (request.AgeRanges.Any(ageRange => ageRange is { Length: > 40 }))
            throw new BadRequestException("Un rango etario no puede superar 40 caracteres.");

        return Enumerable.Range(1, request.PeopleCount)
            .Select(order => new Person
            {
                Order = order,
                Name = request.PersonNames.Count == 0
                    ? $"Persona {order}"
                    : request.PersonNames[order - 1]!.Trim(),
                AgeRange = order <= request.AgeRanges.Count ? request.AgeRanges[order - 1] : null,
            })
            .ToList();
    }

    private static List<ContextCriterion> BuildCriteria(
        Dictionary<string, string?>? criteria, IReadOnlyList<Criterion> catalog)
    {
        if (criteria is null)
            throw new BadRequestException("Los criterios deben ser un objeto.");

        var criteriaById = catalog.ToDictionary(criterion => criterion.Id, StringComparer.Ordinal);
        var rows = new List<ContextCriterion>(criteria.Count);

        foreach (var (id, priority) in criteria)
        {
            if (!criteriaById.TryGetValue(id, out var criterion))
                throw new BadRequestException($"Criterio desconocido: {id}.");

            if (!criterion.Activo)
                throw new BadRequestException($"Criterio no disponible: {id}.");

            if (priority is null || !ValidPriorities.Contains(priority))
                throw new BadRequestException($"Prioridad inválida para el criterio {id}.");

            rows.Add(new ContextCriterion { CriterionId = id, Priority = priority });
        }

        return rows;
    }

    private static List<ContextPlace> BuildPlaces(AnalysisPointsRequest? request)
    {
        if (request is null)
            throw new BadRequestException("Los puntos son obligatorios.");

        if (request.Places.Count > ScoringPlaceLimits.AuthenticatedUser)
            throw new BadRequestException(ScoringPlaceLimits.AuthenticatedUserMessage);

        if (request.RentLimit?.Length > 30)
            throw new BadRequestException("El alquiler máximo no puede superar 30 caracteres.");

        MandatoryScoringLimits.Validate(
            request.RentLimit, request.Operation, request.PurchaseLimitUsd, request.Places.Select(place => place.MaximumTravelTime));

        var places = new List<ContextPlace>(request.Places.Count);

        for (var index = 0; index < request.Places.Count; index++)
        {
            var place = request.Places[index];
            var name = place.Name?.Trim() ?? string.Empty;
            if (name.Length == 0)
                throw new BadRequestException($"El nombre del lugar {index + 1} es obligatorio.");

            places.Add(new ContextPlace
            {
                Order = index + 1,
                Name = name,
                Address = place.Address,
                Latitude = place.Latitude,
                Longitude = place.Longitude,
                Type = place.Type,
                WhoGoes = place.WhoGoes,
                Frequency = place.Frequency,
                Priority = place.Priority,
                MaximumTravelTime = place.MaximumTravelTime,
            });
        }

        return places;
    }

    private static void Replace<T>(ICollection<T> currentItems, IEnumerable<T> newItems)
    {
        currentItems.Clear();
        foreach (var item in newItems)
            currentItems.Add(item);
    }

    public static AnalysisContextResponse ToContextResponse(Context context) =>
        new(
            context.PeopleCount,
            context.People
                .OrderBy(person => person.Order)
                .Select(person => string.IsNullOrWhiteSpace(person.Name) ? $"Persona {person.Order}" : person.Name.Trim())
                .ToList(),
            context.People.OrderBy(person => person.Order).Select(person => person.AgeRange).ToList(),
            context.Mobility?.ToList() ?? [],
            context.Pets?.ToList() ?? []);

    public static Dictionary<string, string> ToCriteriaDictionary(Context context) =>
        context.Criteria.ToDictionary(criterion => criterion.CriterionId, criterion => criterion.Priority, StringComparer.Ordinal);

    public static AnalysisPointsResponse ToPointsResponse(Context context) =>
        new(
            context.MaximumRent,
            context.Places
                .OrderBy(place => place.Order)
                .Select(place => new AnalysisPlaceResponse(
                    place.Id, place.Name, place.Address, place.Latitude, place.Longitude,
                    place.Type, place.WhoGoes, place.Frequency, place.Priority, place.MaximumTravelTime, place.Order))
                .ToList(),
            context.Operation,
            context.MaximumPurchasePriceUsd,
            context.HousingType,
            context.AreaM2);
}
