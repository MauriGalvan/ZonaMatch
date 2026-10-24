using FluentAssertions;
using Moq;
using ZonaMatch.Application.DTOs.GroupInvitation;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.GroupInvitations;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Tests.Application.UseCases.GroupInvitations;

public class CreateInvitationTests
{
    // 1. Declaramos los "Falsos" (Mocks) que vamos a usar
    private readonly Mock<IGroupRepository> _groupRepositoryMock;
    private readonly Mock<IGroupInvitationRepository> _invitationRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<INotificationRepository> _notificationRepositoryMock;
    private readonly Mock<IEmailService> _emailServiceMock;

    // El caso de uso real que vamos a testear
    private readonly CreateInvitation _useCase;

    public CreateInvitationTests()
    {
        // 2. Inicializamos los Mocks
        _groupRepositoryMock = new Mock<IGroupRepository>();
        _invitationRepositoryMock = new Mock<IGroupInvitationRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _notificationRepositoryMock = new Mock<INotificationRepository>();
        _emailServiceMock = new Mock<IEmailService>();

        // 3. Le inyectamos los Mocks a la clase real
        _useCase = new CreateInvitation(
            _groupRepositoryMock.Object,
            _invitationRepositoryMock.Object,
            _userRepositoryMock.Object,
            _notificationRepositoryMock.Object,
            _emailServiceMock.Object,
            TimeProvider.System);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowException_WhenGroupDoesNotExist()
    {
        // Arrange
        var request = new CreateInvitationRequest { GroupId = Guid.NewGuid(), Email = "test@test.com" };
        var currentUserId = Guid.NewGuid();

        // Le decimos al mock: "Cuando alguien llame a GetByIdAsync, devuelve NULL"
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
                // El usuario actual existe, pero solo es "Guest", no "Owner"
                new Participant { UserId = currentUserId, Role = GroupRole.Guest }
            }
        };

        _groupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(request.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _userRepositoryMock
            .Setup(repo => repo.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Usuario?)null);

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
                // Ahora sí, el usuario actual es el "Owner"
                new Participant { UserId = currentUserId, Role = GroupRole.Owner }
            }
        };

        _groupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(request.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        // No necesitamos preparar _invitationRepository ni _emailService 
        // porque sus métodos no devuelven nada (Task), solo ejecutan acciones.

        // Act
        await _useCase.ExecuteAsync(request, currentUserId, CancellationToken.None);

        // Assert
        // Verificamos que se haya llamado a AddAsync exactamente 1 vez con cualquier objeto GroupInvitation
        _invitationRepositoryMock.Verify(
            repo => repo.AddAsync(It.IsAny<GroupInvitation>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Verificamos que se haya enviado el correo exactamente 1 vez a la dirección correcta
        _emailServiceMock.Verify(
            email => email.SendEmailAsync(request.Email, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
