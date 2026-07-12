using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ThreeDManager.Infrastructure.Data;

namespace ThreeDManager.Tests;

public sealed class ThreeDManagerWebFactory : WebApplicationFactory<Program>
{
    private readonly bool _bypassAuthentication;
    private readonly string _databaseName = $"three-d-manager-tests-{Guid.NewGuid():N}";
    private readonly InMemoryDatabaseRoot _databaseRoot = new();

    public ThreeDManagerWebFactory(bool bypassAuthentication = true)
    {
        _bypassAuthentication = bypassAuthentication;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Testing:DatabaseName"] = _databaseName,
                ["Testing:BypassAuthentication"] = _bypassAuthentication.ToString(),
                ["AlphaAccess:Username"] = "alpha-operator",
                ["AlphaAccess:Password"] = "test-only-password"
            });
        });

        // Belt-and-suspenders isolation: pin this factory to its own InMemoryDatabaseRoot
        // instead of relying solely on the (possibly shared) default root keyed by name,
        // so parallel/sequential test runs never leak rows across factory instances.
        builder.ConfigureTestServices(services =>
        {
            if (_bypassAuthentication)
            {
                services.PostConfigure<AuthorizationOptions>(options =>
                    options.FallbackPolicy = null);
            }

            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName, _databaseRoot));
        });
    }

    public async Task SeedAsync(Func<AppDbContext, Task> seed)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await seed(context);
    }

    public HttpClient CreateTestClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }
}
