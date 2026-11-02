using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ZonaMatch.Application.DTOs.GroupInvitation.Requests;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.GroupInvitations;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Tests.Application.UseCases.GroupInvitations;

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

        // 3. Inject the mocks into the real class.
        _useCase = new CreateInvitation(
            _groupRepositoryMock.Object,
            _invitationRepositoryMock.Object,
            _userRepositoryMock.Object,
            _notificationRepositoryMock.Object,
            _emailServiceMock.Object,
            TimeProvider.System,
            NullLogger<CreateInvitation>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowException_WhenGroupDoesNotExist()
    {
        // Arrange
        var request = new CreateInvitationRequest { GroupId = Guid.NewGuid(), Email = "test@test.com" };
        var currentUserId = Guid.NewGuid();

        // Configure the mock to return null when GetByIdAsync is called.
        _groupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(request.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group?)null);

        // Act
        Func<Task> action = async () => await _useCase.ExecuteAsync(request, currentUserId, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<Exception>()
            .WithMessage("El grupo no existe.");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowException_WhenUserIsNotOwner()
    {
        // Arrange
        var request = new CreateInvitationRequest { GroupId = Guid.NewGuid(), Email = "test@test.com" };
        var currentUserId = Guid.NewGuid();

        var group = new Group
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
        await action.Should().ThrowAsync<Exception>()
            .WithMessage("Solo el propietario del grupo puede enviar invitaciones.");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateInvitationAndSendEmail_WhenRequestIsValid()
    {
        // Arrange
        var request = new CreateInvitationRequest { GroupId = Guid.NewGuid(), Email = "test@test.com" };
        var currentUserId = Guid.NewGuid();

        var group = new Group
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
            repo => repo.AddAsync(It.IsAny<GroupInvitation>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify that the email was sent exactly once to the expected address.
        _emailServiceMock.Verify(
            email => email.SendEmailAsync(request.Email, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
