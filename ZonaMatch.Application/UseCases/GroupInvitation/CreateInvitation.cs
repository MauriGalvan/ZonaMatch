using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using ZonaMatch.Application.DTOs.GroupInvitation.Requests;
using ZonaMatch.Application.Emails;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;
using DomainGroup = ZonaMatch.Domain.Entities.Group;
using DomainGroupInvitation = ZonaMatch.Domain.Entities.GroupInvitation;

namespace ZonaMatch.Application.UseCases.GroupInvitation;

public class CreateInvitation
{
    private readonly IGroupRepository _groupRepository;
    private readonly IGroupInvitationRepository _invitationRepository;
    private readonly IUserRepository _userRepository;
    private readonly INotificationRepository _notifications;
    private readonly IEmailService _emailService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CreateInvitation> _logger;
    private readonly FrontendLinks _links;

    public CreateInvitation(
        IGroupRepository groupRepository,
        IGroupInvitationRepository invitationRepository,
        IUserRepository userRepository,
        INotificationRepository notifications,
        IEmailService emailService,
        TimeProvider timeProvider,
        ILogger<CreateInvitation> logger,
        FrontendLinks links)
    {
        _groupRepository = groupRepository;
        _invitationRepository = invitationRepository;
        _userRepository = userRepository;
        _notifications = notifications;
        _emailService = emailService;
        _timeProvider = timeProvider;
        _logger = logger;
        _links = links;
    }

    // currentUserId is read from the JWT by the controller.
    public async Task ExecuteAsync(
        CreateInvitationRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        // 1. Find the group and ensure it exists.
        var group = await _groupRepository.GetByIdAsync(request.GroupId, cancellationToken);
        if (group == null)
        {
            throw new NotFoundException("El grupo no existe.");
        }

        // 2. Verify that the user sending the invitation owns the group.
        bool isOwner = group.Participants.Any(p => p.UserId == currentUserId && GroupRole.Owner == p.Role);
        if (!isOwner)
        {
            throw new ForbiddenException("Solo el propietario del grupo puede enviar invitaciones.");
        }

        if (group.Participants.Count >= DomainGroup.MaximumParticipants)
        {
            throw new ConflictException($"El grupo ya alcanzó el máximo de {DomainGroup.MaximumParticipants} participantes.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var invitedUser = await _userRepository.GetByEmailAsync(email, cancellationToken);

        // The owner is already in the group.
        if (invitedUser?.Id == currentUserId)
            throw new BadRequestException("No podés invitarte a vos mismo.");

        // Inviting a member again would only send a useless email.
        if (invitedUser is not null && group.Participants.Any(p => p.UserId == invitedUser.Id))
            throw new ConflictException("Esa persona ya está en el grupo.");

        var pending = await _invitationRepository.ListPendingAsync(group.Id, now, cancellationToken);
        if (pending.Any(p => string.Equals(p.Email, email, StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException("Ya hay una invitación pendiente para ese email.");

        // Pending invitations cannot outnumber the free places, or some of them could never be accepted.
        if (group.Participants.Count + pending.Count >= DomainGroup.MaximumParticipants)
            throw new ConflictException("Ya hay invitaciones pendientes para todos los lugares libres del grupo.");

        // 3. Create the invitation with a random token.
        var invitation = new DomainGroupInvitation
        {
            GroupId = request.GroupId,
            Email = email,
            Token = Guid.NewGuid(), // This UUIDv4 is included in the invitation link.
            Status = InvitationStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now.AddDays(7) // Invitations are valid for seven days.
        };

        if (invitedUser is not null)
        {
            _notifications.AddRange([
                new UserNotification
                {
                    UserId = invitedUser.Id,
                    Type = NotificationType.GroupInvitation,
                    Title = "Te invitaron a un grupo",
                    Message = $"Te invitaron a unirte a {group.Name}.",
                    Link = $"/invitations/email/{invitation.Token}",
                    CreatedAt = _timeProvider.GetUtcNow()
                }
            ]);
        }

        // 4. Save the invitation to the database.
        await _invitationRepository.AddAsync(invitation, cancellationToken);

        // 5. Send the invitation email.
        string inviteLink = _links.To($"/invitations/email/{invitation.Token}");
        string subject = $"Te invitaron al grupo {group.Name} en ZonaMatch";
        var days = Math.Max(1, (int)Math.Ceiling((invitation.ExpiresAt - now).TotalDays));
        // The group name is typed by a user: it is encoded so it cannot inject HTML
        string body = BrandEmail.Render(new BrandEmailContent(
            Preheader: $"Sumate al grupo {group.Name} para buscar zona juntos.",
            Title: "¡Te invitaron a buscar zona en grupo!",
            Paragraphs:
            [
                $"Te invitaron a sumarte al grupo <strong>{BrandEmail.Encode(group.Name)}</strong> en ZonaMatch.",
                "Cada integrante carga lo que le importa y ZonaMatch arma un ranking de zonas que tiene en cuenta a todos.",
            ],
            Button: new BrandEmailButton("Unirme al grupo", inviteLink),
            Footnote: $"La invitación vence en {days} {(days == 1 ? "día" : "días")}. Si no esperabas este mail, podés ignorarlo."));

        await _emailService.SendEmailAsync(request.Email, subject, body, cancellationToken);

        // The recipient address is not logged on purpose
        _logger.LogInformation("User {UserId} sent an email invitation to group {GroupId}", currentUserId, group.Id);
    }

}
