using ZonaMatch.Application.Emails;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.DTOs.GroupInvitation.Requests;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.GroupInvitation;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;
using DomainGroup = ZonaMatch.Domain.Entities.Group;
using DomainGroupInvitation = ZonaMatch.Domain.Entities.GroupInvitation;

namespace ZonaMatch.Tests.UseCases.GroupInvitation;

public class CreateInvitationTests
{
    // 1. Declare the mocks used by this test.
    private readonly Mock<IGroupRepository> _groupRepositoryMock;
    private readonly Mock<IGroupInvitationRepository> _invitationRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<INotificationRepository> _notificationRepositoryMock;
    private readonly Mock<IEmailService> _emailServiceMock;

    // The real use case under test.
    private readonly CreateInvitation _useCase;

    public CreateInvitationTests()
    {
        // 2. Initialize the mocks.
        _groupRepositoryMock = new Mock<IGroupRepository>();
        _invitationRepositoryMock = new Mock<IGroupInvitationRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _notificationRepositoryMock = new Mock<INotificationRepository>();
        _emailServiceMock = new Mock<IEmailService>();
        _invitationRepositoryMock
            .Setup(repository => repository.ListPendingAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // 3. Inject the mocks into the real class.
        _useCase = new CreateInvitation(
            _groupRepositoryMock.Object,
            _invitationRepositoryMock.Object,
            _userRepositoryMock.Object,
            _notificationRepositoryMock.Object,
            _emailServiceMock.Object,
            TimeProvider.System,
            NullLogger<CreateInvitation>.Instance,
            new FrontendLinks("http://localhost:5173/"));
    }

    [Fact]
    public async Task CreateInvitation_WhenGroupDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var request = new CreateInvitationRequest { GroupId = Guid.NewGuid(), Email = "test@test.com" };
        var currentUserId = Guid.NewGuid();

        // Configure the mock to return null when GetByIdAsync is called.
        _groupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(request.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainGroup?)null);

        // Act
        Func<Task> action = async () => await _useCase.ExecuteAsync(request, currentUserId, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<NotFoundException>()
            .WithMessage("El grupo no existe.");
    }

    [Fact]
    public async Task CreateInvitation_WhenCurrentUserIsNotOwner_ThrowsForbiddenException()
    {
        // Arrange
        var request = new CreateInvitationRequest { GroupId = Guid.NewGuid(), Email = "test@test.com" };
        var currentUserId = Guid.NewGuid();

        var group = new DomainGroup
        {
            Id = request.GroupId,
            Name = "Grupo Test",
            Participants = new List<Participant>
            {
                // The current user exists, but is a "Guest", not the "Owner".
                new Participant { UserId = currentUserId, Role = GroupRole.Guest }
            }
        };

        _groupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(request.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _userRepositoryMock
            .Setup(repo => repo.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        Func<Task> action = async () => await _useCase.ExecuteAsync(request, currentUserId, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Solo el propietario del grupo puede enviar invitaciones.");
    }

    [Fact]
    public async Task CreateInvitation_WhenRequestIsValid_CreatesInvitationAndSendsEmail()
    {
        // Arrange
        var request = new CreateInvitationRequest { GroupId = Guid.NewGuid(), Email = "test@test.com" };
        var currentUserId = Guid.NewGuid();

        var group = new DomainGroup
        {
            Id = request.GroupId,
            Name = "Grupo Test",
            Participants = new List<Participant>
            {
                // The current user is the "Owner" in this scenario.
                new Participant { UserId = currentUserId, Role = GroupRole.Owner }
            }
        };

        _groupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(request.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        // No setup is needed for _invitationRepository or _emailService because their
        // methods return a Task without a value and only perform actions.

        // Act
        await _useCase.ExecuteAsync(request, currentUserId, CancellationToken.None);

        // Assert
        // Verify that AddAsync was called exactly once with any GroupInvitation.
        _invitationRepositoryMock.Verify(
            repo => repo.AddAsync(It.IsAny<DomainGroupInvitation>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify that the email was sent exactly once to the expected address.
        _emailServiceMock.Verify(
            email => email.SendEmailAsync(request.Email, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateInvitation_WhenGroupIsFull_ThrowsConflictWithoutCreatingInvitationOrSendingEmail()
    {
        var request = new CreateInvitationRequest { GroupId = Guid.NewGuid(), Email = "guest@example.com" };
        var ownerId = Guid.NewGuid();
        var group = new DomainGroup
        {
            Id = request.GroupId,
            Name = "Weekend search",
            Participants = Enumerable.Range(0, DomainGroup.MaximumParticipants)
                .Select(index => new Participant
                {
                    UserId = index == 0 ? ownerId : Guid.NewGuid(),
                    Role = index == 0 ? GroupRole.Owner : GroupRole.Guest
                })
                .ToList()
        };
        _groupRepositoryMock
            .Setup(repository => repository.GetByIdAsync(request.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        Func<Task> action = () => _useCase.ExecuteAsync(request, ownerId, CancellationToken.None);

        await action.Should().ThrowAsync<ConflictException>();
        _invitationRepositoryMock.Verify(
            repository => repository.AddAsync(It.IsAny<DomainGroupInvitation>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _emailServiceMock.Verify(
            email => email.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateInvitation_WhenRecipientHasAnAccount_AddsAnInAppNotification()
    {
        var request = new CreateInvitationRequest { GroupId = Guid.NewGuid(), Email = "guest@example.com" };
        var ownerId = Guid.NewGuid();
        var invitedUser = new User { Id = Guid.NewGuid(), Email = request.Email };
        var group = new DomainGroup
        {
            Id = request.GroupId,
            Name = "Weekend search",
            Participants = [new Participant { UserId = ownerId, Role = GroupRole.Owner }]
        };
        _groupRepositoryMock
            .Setup(repository => repository.GetByIdAsync(request.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _userRepositoryMock
            .Setup(repository => repository.GetByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitedUser);

        await _useCase.ExecuteAsync(request, ownerId, CancellationToken.None);

        _notificationRepositoryMock.Verify(repository => repository.AddRange(
            It.Is<IEnumerable<UserNotification>>(notifications =>
                notifications.Count() == 1 &&
                notifications.Single().UserId == invitedUser.Id &&
                notifications.Single().Type == NotificationType.GroupInvitation &&
                notifications.Single().Link.StartsWith("/invitations/email/"))), Times.Once);
    }

    [Fact]
    public async Task CreateInvitation_WhenRecipientIsTheOrganizer_ThrowsBadRequestWithoutCreatingInvitation()
    {
        var request = new CreateInvitationRequest { GroupId = Guid.NewGuid(), Email = "organizer@example.com" };
        var organizerId = Guid.NewGuid();
        var group = OwnedGroup(request.GroupId, organizerId);
        _groupRepositoryMock
            .Setup(repository => repository.GetByIdAsync(request.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _userRepositoryMock
            .Setup(repository => repository.GetByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = organizerId, Email = request.Email });

        var action = () => _useCase.ExecuteAsync(request, organizerId, CancellationToken.None);

        await action.Should().ThrowAsync<BadRequestException>().WithMessage("No podés invitarte a vos mismo.");
        _invitationRepositoryMock.Verify(
            repository => repository.AddAsync(It.IsAny<DomainGroupInvitation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateInvitation_WhenRecipientIsAlreadyAMember_ThrowsConflictWithoutSendingEmail()
    {
        var request = new CreateInvitationRequest { GroupId = Guid.NewGuid(), Email = "member@example.com" };
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var group = OwnedGroup(request.GroupId, ownerId);
        group.Participants.Add(new Participant { UserId = memberId, Role = GroupRole.Guest });
        _groupRepositoryMock
            .Setup(repository => repository.GetByIdAsync(request.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _userRepositoryMock
            .Setup(repository => repository.GetByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = memberId, Email = request.Email });

        var action = () => _useCase.ExecuteAsync(request, ownerId, CancellationToken.None);

        await action.Should().ThrowAsync<ConflictException>().WithMessage("Esa persona ya está en el grupo.");
        _emailServiceMock.Verify(
            email => email.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateInvitation_WhenEmailAlreadyHasAPendingInvitation_ThrowsConflict()
    {
        var request = new CreateInvitationRequest { GroupId = Guid.NewGuid(), Email = " Ana@Example.com " };
        var ownerId = Guid.NewGuid();
        _groupRepositoryMock
            .Setup(repository => repository.GetByIdAsync(request.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnedGroup(request.GroupId, ownerId));
        _invitationRepositoryMock
            .Setup(repository => repository.ListPendingAsync(request.GroupId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DomainGroupInvitation { GroupId = request.GroupId, Email = "ana@example.com" }]);

        var action = () => _useCase.ExecuteAsync(request, ownerId, CancellationToken.None);

        await action.Should().ThrowAsync<ConflictException>().WithMessage("Ya hay una invitación pendiente para ese email.");
    }

    [Fact]
    public async Task CreateInvitation_WhenPendingInvitationsFillTheFreePlaces_ThrowsConflict()
    {
        var request = new CreateInvitationRequest { GroupId = Guid.NewGuid(), Email = "new@example.com" };
        var ownerId = Guid.NewGuid();
        _groupRepositoryMock
            .Setup(repository => repository.GetByIdAsync(request.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnedGroup(request.GroupId, ownerId));
        // The owner plus one pending invitation per free place
        var pending = Enumerable.Range(1, DomainGroup.MaximumParticipants - 1)
            .Select(index => new DomainGroupInvitation { GroupId = request.GroupId, Email = $"guest{index}@example.com" })
            .ToList();
        _invitationRepositoryMock
            .Setup(repository => repository.ListPendingAsync(request.GroupId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pending);

        var action = () => _useCase.ExecuteAsync(request, ownerId, CancellationToken.None);

        await action.Should().ThrowAsync<ConflictException>().WithMessage("Ya hay invitaciones pendientes para todos los lugares libres del grupo.");
    }

    [Fact]
    public async Task CreateInvitation_WhenRequestIsValid_SavesTheNormalizedEmailAndCreationDate()
    {
        var request = new CreateInvitationRequest { GroupId = Guid.NewGuid(), Email = "  Ana@Example.COM " };
        var ownerId = Guid.NewGuid();
        _groupRepositoryMock
            .Setup(repository => repository.GetByIdAsync(request.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnedGroup(request.GroupId, ownerId));
        DomainGroupInvitation? saved = null;
        _invitationRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<DomainGroupInvitation>(), It.IsAny<CancellationToken>()))
            .Callback<DomainGroupInvitation, CancellationToken>((invitation, _) => saved = invitation)
            .Returns(Task.CompletedTask);

        await _useCase.ExecuteAsync(request, ownerId, CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.Email.Should().Be("ana@example.com");
        saved.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        saved.ExpiresAt.Should().BeCloseTo(saved.CreatedAt.AddDays(7), TimeSpan.FromSeconds(1));
    }

    private static DomainGroup OwnedGroup(Guid groupId, Guid ownerId) => new()
    {
        Id = groupId,
        Name = "Weekend search",
        Participants = [new Participant { UserId = ownerId, Role = GroupRole.Owner }]
    };
}
