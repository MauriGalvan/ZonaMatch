using System.Globalization;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Puntaje;

namespace ZonaMatch.Application.UseCases.Groups;

// Starting values of the group budget, taken from the members' profiles:
// - the group buys when every member with a profile buys; otherwise it rents
// - the monthly budget and the purchase cap add up the members that search the same way (shared costs)
// - the property type is suggested only when every member that chose one chose the same
// - the surface is the largest one a member asked for: the group lives together
public sealed record GroupHousingSuggestion(
    string Operation, decimal MonthlyBudget, decimal? MaxPurchaseUsd, string? PropertyType, int? SurfaceM2 = null)
{
    public static GroupHousingSuggestion From(IReadOnlyList<Contexto> profiles)
    {
        var operation = profiles.Count > 0 && profiles.All(profile => profile.Operacion == Operaciones.Compra)
            ? Operaciones.Compra
            : Operaciones.Alquiler;
        var sameSearch = profiles
            .Where(profile => (profile.Operacion == Operaciones.Compra) == (operation == Operaciones.Compra))
            .ToArray();

        var monthly = sameSearch.Select(profile => ParseAmount(profile.AlquilerMaximo) ?? 0).Sum();
        var purchase = operation == Operaciones.Compra ? sameSearch.Sum(profile => profile.CompraMaximaUsd ?? 0) : 0;
        var types = profiles
            .Select(profile => profile.TipoVivienda)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var surface = profiles.Max(profile => profile.SuperficieM2);

        return new GroupHousingSuggestion(
            operation, monthly, purchase > 0 ? purchase : null, types.Length == 1 ? types[0] : null, surface);
    }

    private static decimal? ParseAmount(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantValue))
            return invariantValue;

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("es-AR"), out var localValue)
            ? localValue
            : null;
    }
}
