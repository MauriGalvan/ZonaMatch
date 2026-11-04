namespace ZonaMatch.Domain.Common;

/// <summary>Review ratings and topics that can be selected for a zone review.</summary>
public static class ReviewAspect
{
    // These values are part of the existing client contract and remain unchanged.
    public const string Safety = "seguridad";
    public const string PublicTransport = "transporte";
    public const string Connectivity = "conectividad";
    public const string Shopping = "comercios";
    public const string GreenSpaces = "espacios-verdes";

    public static readonly IReadOnlyList<string> All =
        [Safety, PublicTransport, Connectivity, Shopping, GreenSpaces];

    /// <summary>Normalizes distinct codes and returns them in the standard order.</summary>
    public static bool TryNormalize(
        IEnumerable<string>? codes,
        out IReadOnlyList<string> normalizedCodes,
        out string error)
    {
        var requestedCodes = (codes ?? [])
            .Select(code => code.Trim().ToLowerInvariant())
            .Where(code => code.Length > 0)
            .ToHashSet();

        var unknownCodes = requestedCodes.Except(All).ToList();
        if (unknownCodes.Count > 0)
        {
            normalizedCodes = [];
            error = $"Tema desconocido: {string.Join(", ", unknownCodes)}. Validos: {string.Join(", ", All)}.";
            return false;
        }

        normalizedCodes = All.Where(requestedCodes.Contains).ToList();
        error = string.Empty;
        return true;
    }
}
