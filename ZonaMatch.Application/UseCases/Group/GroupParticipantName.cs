using System.Globalization;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Group;

internal static class GroupParticipantName
{
    public static string DisplayName(Participant participant)
    {
        var firstName = participant.User.FirstName?.Trim();
        var lastName = participant.User.LastName?.Trim();

        return string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName)
            ? "Participante"
            : $"{firstName} {lastName}";
    }

    public static string Initials(User user)
    {
        var firstName = user.FirstName?.Trim();
        var lastName = user.LastName?.Trim();
        if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName))
            return "?";

        return $"{StringInfo.GetNextTextElement(firstName)}{StringInfo.GetNextTextElement(lastName)}".ToUpperInvariant();
    }
}
