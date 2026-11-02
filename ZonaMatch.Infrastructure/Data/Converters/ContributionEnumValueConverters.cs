using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Infrastructure.Data.Converters;

public sealed class ContributionTypeValueConverter : ValueConverter<ContributionType, string>
{
    public ContributionTypeValueConverter()
        : base(type => ContributionEnumValues.ToStorage(type), value => ContributionEnumValues.FromStorageContributionType(value))
    {
    }
}

public sealed class ContributionStatusValueConverter : ValueConverter<ContributionStatus, string>
{
    public ContributionStatusValueConverter()
        : base(status => ContributionEnumValues.ToStorage(status), value => ContributionEnumValues.FromStorageContributionStatus(value))
    {
    }
}

public sealed class CorrectionReasonValueConverter : ValueConverter<CorrectionReason, string>
{
    public CorrectionReasonValueConverter()
        : base(reason => ContributionEnumValues.ToStorage(reason), value => ContributionEnumValues.FromStorageCorrectionReason(value))
    {
    }
}

internal static class ContributionEnumValues
{
    public static string ToStorage(ContributionType type) => type switch
    {
        ContributionType.NewPoint => "PuntoNuevo",
        ContributionType.Correction => "Correccion",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown contribution type.")
    };

    public static ContributionType FromStorageContributionType(string value) => value switch
    {
        "PuntoNuevo" => ContributionType.NewPoint,
        "Correccion" => ContributionType.Correction,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown stored contribution type.")
    };

    public static string ToStorage(ContributionStatus status) => status switch
    {
        ContributionStatus.Pending => "Pendiente",
        ContributionStatus.Approved => "Aprobado",
        ContributionStatus.Rejected => "Rechazado",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown contribution status.")
    };

    public static ContributionStatus FromStorageContributionStatus(string value) => value switch
    {
        "Pendiente" => ContributionStatus.Pending,
        "Aprobado" => ContributionStatus.Approved,
        "Rechazado" => ContributionStatus.Rejected,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown stored contribution status.")
    };

    public static string ToStorage(CorrectionReason reason) => reason switch
    {
        CorrectionReason.Closed => "Cerro",
        CorrectionReason.Name => "Nombre",
        CorrectionReason.Location => "Ubicacion",
        CorrectionReason.Category => "Categoria",
        CorrectionReason.Other => "Otro",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown correction reason.")
    };

    public static CorrectionReason FromStorageCorrectionReason(string value) => value switch
    {
        "Cerro" => CorrectionReason.Closed,
        "Nombre" => CorrectionReason.Name,
        "Ubicacion" => CorrectionReason.Location,
        "Categoria" => CorrectionReason.Category,
        "Otro" => CorrectionReason.Other,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown stored correction reason.")
    };
}
