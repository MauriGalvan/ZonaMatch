using FluentAssertions;
using ZonaMatch.Application.Emails;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Admin;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Tests.UseCases.Community;

namespace ZonaMatch.Tests.UseCases.Admin;

public class SendNameReminderTests
{
    private readonly User _withoutName = new() { Email = "ana@example.com" };
    private readonly User _withLastNameOnly = new() { Email = "beto@example.com", LastName = "Gómez" };
    private readonly User _complete = new() { Email = "carla@example.com", FirstName = "Carla", LastName = "Pérez" };
    private readonly CapturingEmailService _email = new();
    private readonly SendNameReminder _useCase;

    public SendNameReminderTests()
    {
        _useCase = new SendNameReminder(
            new FakeAdminUserRepository(_withoutName, _withLastNameOnly, _complete), _email, new FrontendLinks("https://zonamatch.com.ar"));
    }

    [Fact]
    public async Task SendReminder_WhenTheNameIsMissing_EmailsTheUserWithALinkToSignIn()
    {
        await _useCase.ExecuteAsync(_withoutName.Id);

        var sent = _email.Sent.Should().ContainSingle().Subject;
        sent.To.Should().Be("ana@example.com");
        sent.Subject.Should().Be(SendNameReminder.Subject);
        sent.Body.Should().Contain("https://zonamatch.com.ar/login");
    }

    [Fact]
    public async Task SendReminder_WhenOnlyTheFirstNameIsMissing_StillSendsIt()
    {
        await _useCase.ExecuteAsync(_withLastNameOnly.Id);

        _email.Sent.Should().ContainSingle().Which.To.Should().Be("beto@example.com");
    }

    [Fact]
    public async Task SendReminder_WhenTheUserAlreadyHasBothNames_ThrowsBadRequestWithoutSending()
    {
        var act = () => _useCase.ExecuteAsync(_complete.Id);

        await act.Should().ThrowAsync<BadRequestException>();
        _email.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SendReminder_WhenTheEmailFails_ThrowsServiceUnavailable()
    {
        _email.FailingRecipients.Add("ana@example.com");

        var act = () => _useCase.ExecuteAsync(_withoutName.Id);

        await act.Should().ThrowAsync<ServiceUnavailableException>();
    }

    [Fact]
    public async Task SendReminder_WhenTheUserDoesNotExist_ThrowsNotFound()
    {
        var act = () => _useCase.ExecuteAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
