using Faturas.Domain.Faturas;
using Microsoft.EntityFrameworkCore;

namespace Faturas.Infrastructure.Persistence;

public class FaturasDbContext : DbContext
{
    public FaturasDbContext(DbContextOptions<FaturasDbContext> options) : base(options) { }

    public DbSet<Fatura> Faturas => Set<Fatura>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FaturasDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
