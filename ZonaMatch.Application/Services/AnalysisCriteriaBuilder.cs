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

        context.Personas = request.Context.PeopleCount;
        context.Mascotas = request.Context.Pets.Distinct(StringComparer.Ordinal).ToArray();
        context.Movilidad = request.Context.Mobility.Distinct(StringComparer.Ordinal).ToArray();
        context.AlquilerMaximo = request.Points.RentLimit;
        context.Operacion = operation;
        context.CompraMaximaUsd = purchaseLimit;
        context.TipoVivienda = propertyType;
        context.SuperficieM2 = areaM2;

        Replace(context.Integrantes, householdMembers);
        Replace(context.Criterios, criteria);
        Replace(context.Puntos, places);
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
                Orden = order,
                Nombre = request.PersonNames.Count == 0
                    ? $"Persona {order}"
                    : request.PersonNames[order - 1]!.Trim(),
                Edad = order <= request.AgeRanges.Count ? request.AgeRanges[order - 1] : null,
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

            rows.Add(new ContextCriterion { CriterioId = id, Prioridad = priority });
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
                Orden = index + 1,
                Nombre = name,
                Direccion = place.Address,
                Latitude = place.Latitude,
                Longitude = place.Longitude,
                Tipo = place.Type,
                QuienVa = place.WhoGoes,
                Frecuencia = place.Frequency,
                Prioridad = place.Priority,
                TiempoMax = place.MaximumTravelTime,
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
            context.Personas,
            context.Integrantes
                .OrderBy(person => person.Orden)
                .Select(person => string.IsNullOrWhiteSpace(person.Nombre) ? $"Persona {person.Orden}" : person.Nombre.Trim())
                .ToList(),
            context.Integrantes.OrderBy(person => person.Orden).Select(person => person.Edad).ToList(),
            context.Movilidad?.ToList() ?? [],
            context.Mascotas?.ToList() ?? []);

    public static Dictionary<string, string> ToCriteriaDictionary(Context context) =>
        context.Criterios.ToDictionary(criterion => criterion.CriterioId, criterion => criterion.Prioridad, StringComparer.Ordinal);

    public static AnalysisPointsResponse ToPointsResponse(Context context) =>
        new(
            context.AlquilerMaximo,
            context.Puntos
                .OrderBy(place => place.Orden)
                .Select(place => new AnalysisPlaceResponse(
                    place.Id, place.Nombre, place.Direccion, place.Latitude, place.Longitude,
                    place.Tipo, place.QuienVa, place.Frecuencia, place.Prioridad, place.TiempoMax, place.Orden))
                .ToList(),
            context.Operacion,
            context.CompraMaximaUsd,
            context.TipoVivienda,
            context.SuperficieM2);
}
