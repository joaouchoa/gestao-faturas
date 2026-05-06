using DbUp;
using Faturas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Testcontainers.PostgreSql;

namespace Faturas.Integration.Tests.Infrastructure;

public class IntegrationWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        RunMigrations();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<FaturasDbContext>));

            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<FaturasDbContext>(options =>
                options.UseNpgsql(_container.GetConnectionString()));
        });

        builder.UseEnvironment("Testing");
    }

    private void RunMigrations()
    {
        var upgrader = DeployChanges.To
            .PostgresqlDatabase(_container.GetConnectionString())
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
            throw new InvalidOperationException($"Migração falhou: {result.Error}");
    }

    public new async Task DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }
}
