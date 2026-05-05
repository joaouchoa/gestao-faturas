using Faturas.Domain.Faturas.Repositories;
using Faturas.Infrastructure.Persistence;
using Faturas.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Faturas.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<FaturasDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddScoped<IFaturaRepository, FaturaRepository>();

        return services;
    }
}
