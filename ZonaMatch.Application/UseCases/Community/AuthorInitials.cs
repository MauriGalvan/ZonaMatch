namespace ZonaMatch.Application.UseCases.Community;

// Derives display initials from an email address without exposing the address itself.
public static class AuthorInitials
{
    private static readonly char[] Separators = ['.', '_', '-', '+'];

    public static string GetInitials(string? email)
    {
        var localPart = (email ?? string.Empty).Split('@')[0];
        var nameParts = localPart.Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Where(part => char.IsLetter(part[0]))
            .ToList();

        var initials = nameParts.Count switch
        {
            0 => "?",
            1 => nameParts[0].Length > 1 && char.IsLetter(nameParts[0][1]) ? nameParts[0][..2] : nameParts[0][..1],
            _ => $"{nameParts[0][0]}{nameParts[1][0]}"
        };

        return initials.ToUpperInvariant();
    }
}
