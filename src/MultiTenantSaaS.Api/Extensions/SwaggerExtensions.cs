using System.Reflection;
using Microsoft.OpenApi.Models;
using MultiTenantSaaS.Api.Swagger;
using MultiTenantSaaS.Application.Features.Authentication;

namespace MultiTenantSaaS.Api.Extensions;

public static class SwaggerExtensions
{
    private const string Description = """
        Multi-tenant API starter for a helpdesk-style SaaS product.

        **Data isolation:** shared database, shared schema, separated by `TenantId` and EF Core
        global query filters. One organization cannot see another's data even knowing the exact
        id of a resource: it gets a 404, not a 403.

        ### Try it in 60 seconds

        1. `POST /api/auth/login` with the header `X-Tenant: acme` and
           `{ "email": "admin@acme.ro", "password": "Demo123!parola" }`
        2. Copy `accessToken` from the response.
        3. Click **Authorize** (top right) and paste the token. The other endpoints no longer
           need `X-Tenant`: the organization comes from the token.
        4. `GET /api/tickets` shows only Acme's tickets.
        5. Log in as `admin@globex.ro` (`X-Tenant: globex`) and repeat: a completely separate
           set of data.

        ### Demo accounts

        All use the password `Demo123!parola`.

        | Organization | Plan | User | Role |
        |---|---|---|---|
        | `acme` | Pro | `admin@acme.ro` | TenantAdmin |
        | `acme` | Pro | `maria@acme.ro` | Member |
        | `globex` | Free | `admin@globex.ro` | TenantAdmin |
        | `initech` | Enterprise | `admin@initech.ro` | TenantAdmin |
        | `system` | - | `platform@exemplu.ro` | GlobalAdmin |

        You can also create a new organization with `POST /api/tenants/register`, a public
        endpoint that returns a valid token straight away.
        """;

    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "MultiTenant SaaS API",
                Version = "v1",
                Description = Description,
                Contact = new OpenApiContact { Name = "George Panfil", Url = new Uri("https://github.com/georgepanfil87") }
            });

            // XML comments from both assemblies: controllers live in Api, DTOs in Application.
            IncludeXmlComments(options, Assembly.GetExecutingAssembly());
            IncludeXmlComments(options, typeof(LoginRequest).Assembly);

            var scheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the token only, without the \"Bearer\" prefix.",
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            };

            options.AddSecurityDefinition("Bearer", scheme);

            // Global requirement: the Authorize button applies the token to every operation.
            // Endpoints marked [AllowAnonymous] still work without it.
            options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = [] });

            options.OperationFilter<TenantHeaderOperationFilter>();

            // Ordered by tag so related endpoints stay grouped in the UI.
            options.OrderActionsBy(api => $"{api.GroupName}_{api.RelativePath}");
        });

        return services;
    }

    public static WebApplication UseSwaggerDocumentation(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.DocumentTitle = "MultiTenant SaaS API";
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "MultiTenant SaaS API v1");

            // Schemas stay collapsed: the endpoint list is what you want to see first.
            options.DefaultModelsExpandDepth(-1);
            options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
            options.EnableTryItOutByDefault();
            options.DisplayRequestDuration();
        });

        return app;
    }

    private static void IncludeXmlComments(Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options, Assembly assembly)
    {
        var path = Path.Combine(AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");

        if (File.Exists(path))
        {
            options.IncludeXmlComments(path, includeControllerXmlComments: true);
        }
    }
}
