using Faturas.Domain.Faturas;
using Faturas.Domain.Faturas.Repositories;
using Faturas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Faturas.Infrastructure.Repositories;

public class FaturaRepository : IFaturaRepository
{
    private readonly FaturasDbContext _context;

    public FaturaRepository(FaturasDbContext context) => _context = context;

    public async Task<Fatura?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Faturas
            .Include(f => f.Itens)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Fatura>> ListAsync(FaturaFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _context.Faturas.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.NomeCliente))
            query = query.Where(f => f.NomeCliente.Contains(filter.NomeCliente));

        if (filter.DataInicial.HasValue)
            query = query.Where(f => f.DataEmissao >= filter.DataInicial.Value);

        if (filter.DataFinal.HasValue)
            query = query.Where(f => f.DataEmissao <= filter.DataFinal.Value);

        if (filter.Status.HasValue)
            query = query.Where(f => f.Status == filter.Status.Value);

        return await query
            .Include(f => f.Itens)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Fatura fatura, CancellationToken cancellationToken = default) =>
        await _context.Faturas.AddAsync(fatura, cancellationToken);

    public void Update(Fatura fatura) =>
        _context.Faturas.Update(fatura);

    public void AddItem(ItemFatura item) =>
        _context.Add(item);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken);
}
