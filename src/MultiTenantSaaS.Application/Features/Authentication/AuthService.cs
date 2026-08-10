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
                "Organizația nu a putut fi determinată. Trimite headerul X-Tenant cu slug-ul organizației.");
        }

        var email = request.Email.Trim().ToLowerInvariant();

        // Query filter-ul restrânge automat căutarea la tenantul curent, deci un email
        // existent în altă organizație pur și simplu nu se vede aici.
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        bool isPasswordValid;
        if (user is null)
        {
            // Verificăm oricum, față de un hash-momeală. Fără asta, răspunsul pentru un email
            // inexistent ar veni vizibil mai repede decât pentru unul existent, iar diferența
            // de timp e suficientă ca să enumeri conturile organizației.
            _ = passwordHasher.Verify(request.Password, DummyHash);
            isPasswordValid = false;
        }
        else
        {
            isPasswordValid = passwordHasher.Verify(request.Password, user.PasswordHash);
        }

        if (user is null || !isPasswordValid)
        {
            throw new AuthenticationFailedException("Email sau parolă incorecte.");
        }

        if (!user.IsActive)
        {
            throw new AuthenticationFailedException("Contul este dezactivat.");
        }

        user.RecordSuccessfulLogin();
        await db.SaveChangesAsync(cancellationToken);

        var token = tokenGenerator.Generate(user, tenantContext.TenantSlug ?? string.Empty);

        return new AuthResponse(token.AccessToken, "Bearer", token.ExpiresAtUtc, ToResponse(user));
    }

    public async Task<UserResponse> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId
            ?? throw new AuthenticationFailedException("Cererea nu este autentificată.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Utilizatorul nu mai există.");

        return ToResponse(user);
    }

    public async Task<UserResponse> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!tenantContext.IsResolved)
        {
            throw new BadRequestException("Organizația nu a putut fi determinată.");
        }

        // Entitatea blochează deja escaladarea prin ChangeRole; aici blocăm și calea de creare.
        // Fără asta, un TenantAdmin ar putea crea un GlobalAdmin și ar ieși din propriul tenant.
        if (request.Role == UserRole.GlobalAdmin)
        {
            throw new ForbiddenException("Rolul de GlobalAdmin nu poate fi acordat prin API.");
        }

        var email = request.Email.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new ConflictException($"Există deja un utilizator cu emailul {email} în această organizație.");
        }

        var user = User.Create(email, passwordHasher.Hash(request.Password), request.FullName, request.Role);

        db.Users.Add(user);

        // TenantId este completat de DbContext la salvare, nu de codul de aici.
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(user);
    }

    private static UserResponse ToResponse(User user) =>
        new(user.Id, user.Email, user.FullName, user.Role, user.IsActive, user.LastLoginAtUtc);

    // Hash bine format, cu aceiași parametri ca unul real, deci verificarea lui costă
    // același timp de calcul.
    private static readonly string DummyHash = string.Join('.',
        "210000", Convert.ToBase64String(new byte[16]), Convert.ToBase64String(new byte[32]));
}
