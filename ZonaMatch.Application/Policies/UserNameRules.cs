using System.ComponentModel.DataAnnotations;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Policies;

internal static class UserNameRules
{
    public const string FirstNameRequiredMessage = "El nombre es obligatorio.";
    public const string LastNameRequiredMessage = "El apellido es obligatorio.";
    private const string FirstNameLengthMessage = "El nombre no puede superar los 100 caracteres.";
    private const string LastNameLengthMessage = "El apellido no puede superar los 100 caracteres.";

    public static IEnumerable<ValidationResult> ValidateLength(string? firstName, string? lastName)
    {
        if (firstName?.Trim().Length > User.MaximumNameLength)
            yield return new ValidationResult(FirstNameLengthMessage, [nameof(User.FirstName)]);

        if (lastName?.Trim().Length > User.MaximumNameLength)
            yield return new ValidationResult(LastNameLengthMessage, [nameof(User.LastName)]);
    }

    public static (string FirstName, string LastName) Normalize(string? firstName, string? lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new BadRequestException(FirstNameRequiredMessage);

        if (string.IsNullOrWhiteSpace(lastName))
            throw new BadRequestException(LastNameRequiredMessage);

        var normalizedFirstName = firstName.Trim();
        var normalizedLastName = lastName.Trim();
        if (ValidateLength(normalizedFirstName, normalizedLastName).FirstOrDefault()?.ErrorMessage is { } error)
            throw new BadRequestException(error);

        return (normalizedFirstName, normalizedLastName);
    }
}
