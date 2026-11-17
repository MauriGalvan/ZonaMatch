using ZonaMatch.Application.DTOs.Investor.Requests;
using ZonaMatch.Application.DTOs.Investor.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Application.UseCases.Investor;

// Rules shared by the investor profile use cases.
public static class InvestorProfiles
{
    private static readonly HashSet<string> Priorities = ["baja", "media", "alta"];

    // Investor features need access granted by an administrator, read from the database so it applies at once.
    public static async Task EnsureAccessAsync(IUserRepository users, Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("El usuario no existe.");
        if (!user.HasInvestorAccess)
            throw new ForbiddenException("Necesitás que un administrador habilite tu perfil de inversor.");
    }

    // Copies a valid request into the profile; invalid input leaves it unchanged.
    public static void Apply(Context profile, InvestorProfileRequest request, IReadOnlyList<Domain.Entities.Criterion> catalog)
    {
        var investorCriteria = catalog.Where(criterion => criterion.ForInvestor && criterion.IsActive)
            .ToDictionary(criterion => criterion.Id, StringComparer.Ordinal);
        var criteria = new List<ContextCriterion>();
        foreach (var (id, priority) in request.Criteria ?? [])
        {
            if (!investorCriteria.ContainsKey(id))
                throw new BadRequestException($"Criterio de inversión desconocido: {id}.");
            if (priority is null || !Priorities.Contains(priority))
                throw new BadRequestException($"Prioridad inválida para el criterio {id}.");
            criteria.Add(new ContextCriterion { CriterionId = id, Priority = priority });
        }

        if (criteria.Count == 0)
            throw new BadRequestException("Elegí al menos un criterio de inversión.");
        if (request.PropertyType is not null && !HousingTypes.IsValid(request.PropertyType))
            throw new BadRequestException("El tipo de propiedad no es válido.");
        if (request.CapitalUsd is not { } capital)
            throw new BadRequestException("Indicá el capital disponible en dólares.");

        profile.Operation = HousingOperations.Purchase;
        profile.MaximumPurchasePriceUsd = capital;
        profile.CreditEligible = request.CreditEligible;
        profile.HousingType = request.PropertyType;
        profile.Rooms = request.Rooms;
        profile.PeopleCount = 1;
        profile.Criteria.Clear();
        foreach (var criterion in criteria)
            profile.Criteria.Add(criterion);
    }

    public static InvestorProfileResponse ToResponse(Context? profile) => profile is null
        ? new InvestorProfileResponse(false, new Dictionary<string, string>(), null, false, null, null, null)
        : new InvestorProfileResponse(
            true,
            profile.Criteria.ToDictionary(criterion => criterion.CriterionId, criterion => criterion.Priority),
            profile.MaximumPurchasePriceUsd,
            profile.CreditEligible,
            profile.HousingType,
            profile.Rooms,
            profile.UpdatedAt);
}

public sealed class GetMyInvestorProfile(IUserRepository users, IProfileRepository profiles)
{
    public async Task<InvestorProfileResponse> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await InvestorProfiles.EnsureAccessAsync(users, userId, cancellationToken);
        return InvestorProfiles.ToResponse(await profiles.GetInvestorProfileAsync(userId, cancellationToken));
    }
}

public sealed class SaveMyInvestorProfile(
    IUserRepository users, IProfileRepository profiles, ICriterionRepository criteria, TimeProvider clock)
{
    public async Task<InvestorProfileResponse> ExecuteAsync(
        Guid userId, InvestorProfileRequest request, CancellationToken cancellationToken = default)
    {
        await InvestorProfiles.EnsureAccessAsync(users, userId, cancellationToken);

        var now = clock.GetUtcNow();
        var profile = await profiles.GetInvestorProfileAsync(userId, cancellationToken);
        var isNew = profile is null;
        profile ??= new Context { UserId = userId, Type = ContextType.Investor, CreatedAt = now };

        InvestorProfiles.Apply(profile, request, await criteria.ListAsync(cancellationToken));
        profile.UpdatedAt = now;

        if (isNew)
            profiles.Add(profile);
        await profiles.SaveChangesAsync(cancellationToken);

        return InvestorProfiles.ToResponse(profile);
    }
}
