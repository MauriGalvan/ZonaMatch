using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.GroupLinks;

/// <summary>Returns the active invite token for a group without rotating it.</summary>
public sealed class GetGroupInviteLink
{
    private readonly IGroupRepository _groupRepository;

    public GetGroupInviteLink(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task<Guid> ExecuteAsync(Guid groupId, Guid currentUserId, CancellationToken cancellationToken)
    {
        var group = await _groupRepository.GetByIdAsync(groupId, cancellationToken)
            ?? throw new NoEncontradoException("No se encontró el grupo.");

        if (!group.Participants.Any(participant => participant.UserId == currentUserId))
            throw new AccesoDenegadoException("No pertenecés a este grupo.");

        // Older groups may have received Guid.Empty when InviteToken was added.
        // Create the token once so subsequent reads return the same active link.
        if (group.InviteToken == Guid.Empty)
        {
            group.InviteToken = Guid.NewGuid();
            await _groupRepository.UpdateAsync(group, cancellationToken);
        }

        return group.InviteToken;
    }
}
