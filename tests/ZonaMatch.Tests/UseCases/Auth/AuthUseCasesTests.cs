using System.ComponentModel.DataAnnotations;
using ZonaMatch.Application.DTOs.Auth.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Auth;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Auth;

public sealed class AuthUseCasesTests
{
    private static readonly DateTimeOffset CurrentTime = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeUserRepository _users = new();
    private readonly RegisterUser _registerUser;
    private readonly LoginUser _loginUser;

    public AuthUseCasesTests()
    {
        var passwordHasher = new FakePasswordHasher();
        _registerUser = new RegisterUser(_users, passwordHasher, new FixedTimeProvider(CurrentTime));
        _loginUser = new LoginUser(_users, passwordHasher, new FakeTokenService());
    }

    private static RegisterRequest CreateRegisterRequest(
        string email = "ana@example.com",
        string password = "Secreta123",
        DateOnly? birthDate = null) =>
        new(email, password, password, birthDate ?? new DateOnly(1990, 5, 31));

    [Fact]
    public async Task Register_WithNewEmail_SavesUser()
    {
        var response = await _registerUser.ExecuteAsync(CreateRegisterRequest());

        var savedUser = Assert.Single(_users.SavedUsers);
        Assert.Equal(savedUser.Id, response.Id);
        Assert.Equal("ana@example.com", response.Email);
        Assert.Equal(new DateOnly(1990, 5, 31), response.BirthDate);
        Assert.Equal(CurrentTime, savedUser.FechaCreacion);
    }

    [Fact]
    public async Task Register_NormalizesEmail()
    {
        var response = await _registerUser.ExecuteAsync(CreateRegisterRequest(email: "  Ana@Example.COM "));

        Assert.Equal("ana@example.com", response.Email);
        Assert.Equal("ana@example.com", Assert.Single(_users.SavedUsers).Email);
    }

    [Fact]
    public async Task Register_StoresOnlyThePasswordHash()
    {
        await _registerUser.ExecuteAsync(CreateRegisterRequest());

        var savedUser = Assert.Single(_users.SavedUsers);
        Assert.Equal(FakePasswordHasher.HashPrefix + "Secreta123", savedUser.PasswordHash);
        Assert.NotEqual("Secreta123", savedUser.PasswordHash);
    }

    [Fact]
    public async Task Register_WithExistingEmail_ThrowsConflict()
    {
        await _registerUser.ExecuteAsync(CreateRegisterRequest());

        await Assert.ThrowsAsync<ConflictException>(() => _registerUser.ExecuteAsync(CreateRegisterRequest()));

        Assert.Single(_users.SavedUsers);
    }

    [Fact]
    public async Task Register_WhenDuplicateWinsRace_PropagatesConflict()
    {
        _users.ThrowConflictOnAdd = true;

        await Assert.ThrowsAsync<ConflictException>(() => _registerUser.ExecuteAsync(CreateRegisterRequest()));
    }

    [Fact]
    public async Task Register_WithoutBirthDate_ThrowsBadRequest()
    {
        var request = CreateRegisterRequest() with { BirthDate = null };

        await Assert.ThrowsAsync<BadRequestException>(() => _registerUser.ExecuteAsync(request));

        Assert.Empty(_users.SavedUsers);
    }

    [Fact]
    public void Validate_WhenUserIsUnderage_ReportsBirthDateError()
    {
        var request = CreateRegisterRequest(birthDate: new DateOnly(2015, 3, 10));

        var error = Assert.Single(Validate(request));

        Assert.Equal("Debes ser mayor de edad para registrarte.", error.ErrorMessage);
        Assert.Equal([nameof(RegisterRequest.BirthDate)], error.MemberNames);
    }

    [Fact]
    public void Validate_TheDayBeforeTurning18_ReportsBirthDateError()
    {
        var request = CreateRegisterRequest(birthDate: new DateOnly(2008, 10, 6));

        var error = Assert.Single(Validate(request));

        Assert.Equal([nameof(RegisterRequest.BirthDate)], error.MemberNames);
    }

    [Fact]
    public void Validate_OnThe18thBirthday_IsValid()
    {
        var request = CreateRegisterRequest(birthDate: new DateOnly(2008, 10, 5));

        Assert.Empty(Validate(request));
    }

    [Theory]
    [InlineData(2026, 10, 6)] // tomorrow
    [InlineData(1906, 10, 4)] // more than 120 years ago
    public void Validate_WithImplausibleBirthDate_ReportsInvalidDate(int year, int month, int day)
    {
        var request = CreateRegisterRequest(birthDate: new DateOnly(year, month, day));

        var error = Assert.Single(Validate(request));

        Assert.Equal("La fecha de nacimiento no es valida.", error.ErrorMessage);
    }

    private static List<ValidationResult> Validate(RegisterRequest request)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(request, new FixedTimeServiceProvider(), items: null);
        Validator.TryValidateObject(request, context, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenAndUser()
    {
        await _registerUser.ExecuteAsync(CreateRegisterRequest());

        var result = await _loginUser.ExecuteAsync(new LoginRequest("ana@example.com", "Secreta123"));

        var savedUser = Assert.Single(_users.SavedUsers);
        Assert.Equal(FakeTokenService.TokenPrefix + savedUser.Id, result.AccessToken);
        Assert.Equal(CurrentTime.AddHours(1), result.ExpiresAt);
        Assert.Equal(savedUser.Id, result.User.Id);
        Assert.Equal("ana@example.com", result.User.Email);
    }

    [Fact]
    public async Task Login_NormalizesEmail()
    {
        await _registerUser.ExecuteAsync(CreateRegisterRequest());

        var result = await _loginUser.ExecuteAsync(new LoginRequest("  ANA@Example.com ", "Secreta123"));

        Assert.Equal("ana@example.com", result.User.Email);
    }

    [Fact]
    public async Task Login_WithIncorrectPassword_ThrowsUnauthorized()
    {
        await _registerUser.ExecuteAsync(CreateRegisterRequest());

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _loginUser.ExecuteAsync(new LoginRequest("ana@example.com", "OtraClave999")));
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ThrowsUnauthorized()
    {
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _loginUser.ExecuteAsync(new LoginRequest("nadie@example.com", "Secreta123")));
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public List<Usuario> SavedUsers { get; } = [];
        public bool ThrowConflictOnAdd { get; set; }

        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(SavedUsers.Any(user => user.Email == email));

        public Task<Usuario?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(SavedUsers.FirstOrDefault(user => user.Email == email));

        public Task AddAsync(Usuario user, CancellationToken cancellationToken = default)
        {
            if (ThrowConflictOnAdd)
                throw new ConflictException("El email ya esta registrado.");

            SavedUsers.Add(user);
            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public const string HashPrefix = "hash:";

        public string Hash(string password) => HashPrefix + password;

        public bool Verify(string? hash, string password) => hash is not null && hash == HashPrefix + password;
    }

    private sealed class FakeTokenService : ITokenService
    {
        public const string TokenPrefix = "token:";

        public (string Token, DateTimeOffset ExpiresAt) Generate(Usuario user) =>
            (TokenPrefix + user.Id, CurrentTime.AddHours(1));
    }

    private sealed class FixedTimeServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(TimeProvider) ? new FixedTimeProvider(CurrentTime) : null;
    }

    private sealed class FixedTimeProvider(DateTimeOffset currentTime) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => currentTime;
    }
}
