using DMRS.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace DMRS.IntegrationTests.Harness;

/// <summary>
/// Hosts the real DMRS API in-memory (via <see cref="WebApplicationFactory{TEntryPoint}"/>) against a
/// throwaway PostgreSQL database in a Docker container. This is the closest a test can get to the
/// running system: the actual controllers, repository, EF Core provider, migrations and authorization
/// pipeline all execute — only the identity server is swapped for <see cref="TestAuthHandler"/>.
///
/// The container is created once and shared across a whole test collection (see
/// <c>IntegrationCollection</c>); each test class resets the data so tests stay independent.
/// </summary>
public sealed class DmrsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("dmrs_test")
        .WithUsername("dmrs")
        .WithPassword("dmrs")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Point the app's DefaultConnection at the container. GetConnectionString("DefaultConnection")
        // in Program.cs reads this configuration key, so no DbContext re-registration is needed.
        builder.UseSetting("ConnectionStrings:DefaultConnection", _database.GetConnectionString());

        // Avoid the dev-only Swagger/seed wiring; the tests drive the API directly.
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Swap Keycloak JWT bearer for the header-driven test scheme. Registering authentication
            // again here (after Program) makes TestAuthHandler the default scheme, so [Authorize]
            // resolves to it while the real authorization handlers still run unchanged.
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    /// <summary>Applies EF Core migrations, giving the container the exact production schema.</summary>
    private async Task MigrateAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    /// <summary>
    /// Removes all rows so each test class starts from a known-empty database. Truncating is far
    /// cheaper than recreating the container or re-running migrations between classes.
    /// </summary>
    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            """TRUNCATE "FhirResources", "FhirResourceVersions", "ResourceIndices" RESTART IDENTITY CASCADE;""");
    }

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        await MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _database.DisposeAsync();
    }
}
