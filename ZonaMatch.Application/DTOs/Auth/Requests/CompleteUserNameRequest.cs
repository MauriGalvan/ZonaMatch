using System.ComponentModel.DataAnnotations;
using ZonaMatch.Application.Policies;

namespace ZonaMatch.Application.DTOs.Auth.Requests;

public sealed record CompleteUserNameRequest(
    [Required(ErrorMessage = UserNameRules.FirstNameRequiredMessage)] string? FirstName,
    [Required(ErrorMessage = UserNameRules.LastNameRequiredMessage)] string? LastName) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        UserNameRules.ValidateLength(FirstName, LastName);
}
