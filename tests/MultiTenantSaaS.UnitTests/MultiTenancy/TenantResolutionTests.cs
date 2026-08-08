using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MultiTenantSaaS.Api.Middleware;
using MultiTenantSaaS.Api.MultiTenancy;
using MultiTenantSaaS.Application.Abstractions;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Domain.Enums;
using MultiTenantSaaS.Infrastructure.MultiTenancy;
using Xunit;

namespace MultiTenantSaaS.UnitTests.MultiTenancy;

public sealed class TenantResolutionTests
{
    private static readonly TenantInfo Acme = new(
        Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"), "acme", "Acme", SubscriptionPlan.Free, true, null);

    private static readonly TenantInfo Globex = new(
        Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002"), "globex", "Globex", SubscriptionPlan.Pro, true, null);

    private static readonly TenantInfo Suspended = new(
        Guid.Parse("cccccccc-0000-0000-0000-000000000003"), "suspendat", "Suspendat", SubscriptionPlan.Free, false, null);

    [Fact]
    public async Task Header_ResolvesTenant_ForAnonymousRequest()
    {
        var (context, tenantContext, resolved) = await RunAsync(headerSlug: "acme");

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(Acme.Id, resolved);
        Assert.False(tenantContext.IsResolved); // scope-ul se închide la ieșirea din middleware
    }

    [Fact]
    public async Task Token_WinsOverHeader_WhenBothPresentAndConsistent()
    {
        // Header cu ID-ul tenantului în loc de slug: aceeași organizație, altă formă.
        var (context, _, resolved) = await RunAsync(
            headerSlug: Acme.Id.ToString(),
            tokenTenantId: Acme.Id.ToString());

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(Acme.Id, resolved);
    }

    [Fact]
    public async Task Token_AndHeader_ForDifferentTenants_Returns403()
    {
        // Scenariul de atac: utilizator autentificat în Acme, cere datele Globex prin header.
        var (context, _, resolved) = await RunAsync(
            headerSlug: "globex",
            tokenTenantId: Acme.Id.ToString());

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Null(resolved);
    }

    [Fact]
    public async Task UnknownTenant_Returns404()
    {
        var (context, _, resolved) = await RunAsync(headerSlug: "inexistent");

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Null(resolved);
    }

    [Fact]
    public async Task SuspendedTenant_Returns403()
    {
        var (context, _, resolved) = await RunAsync(headerSlug: "suspendat");

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Null(resolved);
    }

    [Fact]
    public async Task NoIdentifier_PassesThroughWithoutTenant()
    {
        var (context, _, resolved) = await RunAsync();

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Null(resolved);
    }

    [Fact]
    public async Task SkippedPath_IsNotBlockedByBogusHeader()
    {
        var (context, _, resolved) = await RunAsync(headerSlug: "inexistent", path: "/health");

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Null(resolved);
    }

    [Theory]
    [InlineData("acme.api.exemplu.ro", "acme")]
    [InlineData("api.exemplu.ro", null)]        // domeniul de bază nu e un tenant
    [InlineData("a.b.api.exemplu.ro", null)]    // un singur nivel de subdomeniu
    [InlineData("acme.alt-domeniu.ro", null)]   // Host controlat de client, dar necunoscut
    public void Subdomain_ExtractsOnlyValidSingleLabel(string host, string? expected)
    {
        var options = Options.Create(new TenantResolutionOptions
        {
            EnableSubdomainStrategy = true,
            BaseDomain = "api.exemplu.ro"
        });

        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);

        Assert.Equal(expected, new SubdomainTenantResolutionStrategy(options).TryResolve(context));
    }

    private static async Task<(HttpContext Context, ITenantContext TenantContext, Guid? Resolved)> RunAsync(
        string? headerSlug = null,
        string? tokenTenantId = null,
        string path = "/api/tickets")
    {
        var options = Options.Create(new TenantResolutionOptions());
        var tenantContext = new TenantContext();
        Guid? resolvedInsidePipeline = null;

        var context = new DefaultHttpContext
        {
            // Results.Problem() are nevoie de serviciile cererii ca să serializeze răspunsul.
            // În producție le pune ASP.NET Core; aici le construim minimal.
            RequestServices = new ServiceCollection()
                .AddLogging()
                .AddProblemDetails()
                .BuildServiceProvider()
        };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        if (headerSlug is not null)
        {
            context.Request.Headers["X-Tenant"] = headerSlug;
        }

        if (tokenTenantId is not null)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(TenantClaimTypes.TenantId, tokenTenantId)],
                authenticationType: "Test"));
        }

        var middleware = new TenantResolutionMiddleware(
            next: _ =>
            {
                resolvedInsidePipeline = tenantContext.TenantId;
                return Task.CompletedTask;
            },
            options,
            NullLogger<TenantResolutionMiddleware>.Instance);

        await middleware.InvokeAsync(
            context,
            [new ClaimTenantResolutionStrategy(), new HeaderTenantResolutionStrategy(options)],
            new FakeTenantStore(),
            tenantContext);

        return (context, tenantContext, resolvedInsidePipeline);
    }

    private sealed class FakeTenantStore : ITenantStore
    {
        public Task<TenantInfo?> FindAsync(string identifier, CancellationToken cancellationToken = default)
        {
            TenantInfo? match = identifier.ToLowerInvariant() switch
            {
                "acme" => Acme,
                "globex" => Globex,
                "suspendat" => Suspended,
                var value when value == Acme.Id.ToString() => Acme,
                var value when value == Globex.Id.ToString() => Globex,
                _ => null
            };

            return Task.FromResult(match);
        }
    }
}
