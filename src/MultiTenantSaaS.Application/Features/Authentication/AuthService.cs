using Microsoft.EntityFrameworkCore;
using MultiTenantSaaS.Application.Abstractions;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Domain.Entities;
using MultiTenantSaaS.Domain.Enums;

namespace MultiTenantSaaS.Application.Features.Authentication;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<UserResponse> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
}

public sealed class AuthService(
    IApplicationDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator tokenGenerator,
    ITenantContext tenantContext,
    ICurrentUser currentUser) : IAuthService
{
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!tenantContext.IsResolved)
        {
            throw new BadRequestException(
                "The organization could not be determined. Send the X-Tenant header with the organization slug.");
        }

        var email = request.Email.Trim().ToLowerInvariant();

        // The global query filter scopes this to the current tenant, so an email that exists
        // in another organization simply is not visible here.
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        bool isPasswordValid;
        if (user is null)
        {
            // Verify anyway, against a decoy hash. Otherwise the response for an unknown email
            // returns noticeably faster, and that timing difference is enough to enumerate accounts.
            _ = passwordHasher.Verify(request.Password, DummyHash);
            isPasswordValid = false;
        }
        else
        {
            isPasswordValid = passwordHasher.Verify(request.Password, user.PasswordHash);
        }

        if (user is null || !isPasswordValid)
        {
            throw new AuthenticationFailedException("Wrong email or password.");
        }

        if (!user.IsActive)
        {
            throw new AuthenticationFailedException("This account is disabled.");
        }

        user.RecordSuccessfulLogin();
        await db.SaveChangesAsync(cancellationToken);

        var token = tokenGenerator.Generate(user, tenantContext.TenantSlug ?? string.Empty);

        return new AuthResponse(token.AccessToken, "Bearer", token.ExpiresAtUtc, ToResponse(user));
    }

    public async Task<UserResponse> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId
            ?? throw new AuthenticationFailedException("The request is not authenticated.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("The user no longer exists.");

        return ToResponse(user);
    }

    public async Task<UserResponse> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!tenantContext.IsResolved)
        {
            throw new BadRequestException("The organization could not be determined.");
        }

        // The entity already blocks escalation via ChangeRole; this closes the creation path too.
        if (request.Role == UserRole.GlobalAdmin)
        {
            throw new ForbiddenException("The GlobalAdmin role cannot be granted through the API.");
        }

        var email = request.Email.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new ConflictException($"A user with the email {email} already exists in this organization.");
        }

        var user = User.Create(email, passwordHasher.Hash(request.Password), request.FullName, request.Role);

        db.Users.Add(user);

        // TenantId is filled in by the DbContext on save, not here.
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(user);
    }

    private static UserResponse ToResponse(User user) =>
        new(user.Id, user.Email, user.FullName, user.Role, user.IsActive, user.LastLoginAtUtc);

    // Well-formed hash with the same parameters as a real one, so verifying it costs the
    // same amount of work.
    private static readonly string DummyHash = string.Join('.',
        "210000", Convert.ToBase64String(new byte[16]), Convert.ToBase64String(new byte[32]));
}
