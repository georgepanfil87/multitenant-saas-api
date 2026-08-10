using Microsoft.EntityFrameworkCore;
using MultiTenantSaaS.Application.Abstractions;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Application.Features.Authentication;
using MultiTenantSaaS.Domain.Entities;
using MultiTenantSaaS.Domain.Enums;
using MultiTenantSaaS.Infrastructure.Identity;
using MultiTenantSaaS.Infrastructure.MultiTenancy;
using MultiTenantSaaS.Infrastructure.Persistence;
using Xunit;

namespace MultiTenantSaaS.UnitTests.Authorization;

public sealed class AuthServiceTests : IDisposable
{
    private static readonly Guid AcmeId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid GlobexId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private const string Password = "Parola-Sigura-123";

    private readonly TenantContext _tenantContext = new();
    private readonly ApplicationDbContext _db;
    private readonly Pbkdf2PasswordHasher _hasher = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"auth-{Guid.NewGuid()}").Options,
            _tenantContext);

        _sut = new AuthService(_db, _hasher, new FakeTokenGenerator(), _tenantContext, _currentUser);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsToken()
    {
        await SeedUserAsync(AcmeId, "george@acme.ro", UserRole.TenantAdmin);

        using (_tenantContext.BeginScope(AcmeId, "acme"))
        {
            var response = await _sut.LoginAsync(new LoginRequest { Email = "george@acme.ro", Password = Password });

            Assert.Equal("Bearer", response.TokenType);
            Assert.Equal("george@acme.ro", response.User.Email);
            Assert.Equal(UserRole.TenantAdmin, response.User.Role);
            Assert.NotNull(response.User.LastLoginAtUtc);
        }
    }

    [Fact]
    public async Task Login_WithSameEmailButWrongTenant_Fails()
    {
        // The scenario that justifies (TenantId, Email) uniqueness: the account exists in Acme,
        // but the login request targets Globex.
        await SeedUserAsync(AcmeId, "george@acme.ro", UserRole.TenantAdmin);

        using (_tenantContext.BeginScope(GlobexId, "globex"))
        {
            await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
                _sut.LoginAsync(new LoginRequest { Email = "george@acme.ro", Password = Password }));
        }
    }

    [Fact]
    public async Task Login_WithWrongPassword_GivesSameErrorAsUnknownEmail()
    {
        await SeedUserAsync(AcmeId, "george@acme.ro", UserRole.Member);

        using (_tenantContext.BeginScope(AcmeId, "acme"))
        {
            var wrongPassword = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
                _sut.LoginAsync(new LoginRequest { Email = "george@acme.ro", Password = "gresita" }));

            var unknownEmail = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
                _sut.LoginAsync(new LoginRequest { Email = "nimeni@acme.ro", Password = "gresita" }));

            // Identical messages: otherwise the API confirms which addresses exist.
            Assert.Equal(wrongPassword.Message, unknownEmail.Message);
        }
    }

    [Fact]
    public async Task Login_WithoutResolvedTenant_ReturnsBadRequest()
    {
        await SeedUserAsync(AcmeId, "george@acme.ro", UserRole.Member);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _sut.LoginAsync(new LoginRequest { Email = "george@acme.ro", Password = Password }));
    }

    [Fact]
    public async Task Login_WithDeactivatedAccount_Fails()
    {
        var userId = await SeedUserAsync(AcmeId, "george@acme.ro", UserRole.Member);

        using (_tenantContext.BeginScope(AcmeId, "acme"))
        {
            var user = await _db.Users.SingleAsync(u => u.Id == userId);
            user.Deactivate();
            await _db.SaveChangesAsync();

            await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
                _sut.LoginAsync(new LoginRequest { Email = "george@acme.ro", Password = Password }));
        }
    }

    [Fact]
    public async Task CreateUser_StampsCurrentTenant()
    {
        using (_tenantContext.BeginScope(AcmeId, "acme"))
        {
            var created = await _sut.CreateUserAsync(new CreateUserRequest
            {
                Email = "Nou@Acme.RO",
                Password = Password,
                FullName = "Utilizator Nou"
            });

            var entity = await _db.Users.SingleAsync(u => u.Id == created.Id);

            Assert.Equal(AcmeId, entity.TenantId);
            Assert.Equal("nou@acme.ro", entity.Email); // normalized in the domain
            Assert.Equal(UserRole.Member, entity.Role);
            Assert.NotEqual(Password, entity.PasswordHash);
        }
    }

    [Fact]
    public async Task CreateUser_WithGlobalAdminRole_IsForbidden()
    {
        using (_tenantContext.BeginScope(AcmeId, "acme"))
        {
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _sut.CreateUserAsync(new CreateUserRequest
                {
                    Email = "atacator@acme.ro",
                    Password = Password,
                    FullName = "Escaladare",
                    Role = UserRole.GlobalAdmin
                }));
        }
    }

    [Fact]
    public async Task CreateUser_WithDuplicateEmailInSameTenant_Conflicts()
    {
        await SeedUserAsync(AcmeId, "george@acme.ro", UserRole.Member);

        using (_tenantContext.BeginScope(AcmeId, "acme"))
        {
            await Assert.ThrowsAsync<ConflictException>(() =>
                _sut.CreateUserAsync(new CreateUserRequest
                {
                    Email = "george@acme.ro",
                    Password = Password,
                    FullName = "Duplicat"
                }));
        }
    }

    [Fact]
    public async Task CreateUser_WithSameEmailInAnotherTenant_IsAllowed()
    {
        await SeedUserAsync(AcmeId, "consultant@extern.ro", UserRole.Member);

        using (_tenantContext.BeginScope(GlobexId, "globex"))
        {
            var created = await _sut.CreateUserAsync(new CreateUserRequest
            {
                Email = "consultant@extern.ro",
                Password = Password,
                FullName = "Același consultant, altă organizație"
            });

            Assert.Equal("consultant@extern.ro", created.Email);
        }

        Assert.Equal(2, await _db.Users.IgnoreQueryFilters().CountAsync());
    }

    private async Task<Guid> SeedUserAsync(Guid tenantId, string email, UserRole role)
    {
        using (_tenantContext.BeginScope(tenantId))
        {
            var user = User.Create(email, _hasher.Hash(Password), "Utilizator Test", role);
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            _db.Entry(user).State = EntityState.Detached;
            return user.Id;
        }
    }

    private sealed class FakeTokenGenerator : IJwtTokenGenerator
    {
        public GeneratedToken Generate(User user, string tenantSlug) =>
            new($"token-pentru-{user.Email}", DateTime.UtcNow.AddHours(1));
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; set; }

        public string? Email { get; set; }

        public UserRole? Role { get; set; }

        public bool IsAuthenticated => UserId is not null;
    }
}
