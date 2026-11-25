using System.Globalization;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Community;

internal sealed record CommunityAuthor(string? Name, string Initials)
{
    public static CommunityAuthor FromUser(User? user, string? fallbackEmail = null)
    {
        var firstName = user?.FirstName?.Trim();
        var lastName = user?.LastName?.Trim();

        if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName))
            return new CommunityAuthor(null, AuthorInitials.GetInitials(user?.Email ?? fallbackEmail));

        var initials = $"{StringInfo.GetNextTextElement(firstName)}{StringInfo.GetNextTextElement(lastName)}";
        return new CommunityAuthor($"{firstName} {lastName}", initials.ToUpperInvariant());
    }
}
