namespace Faturas.Domain.Faturas.Repositories;

public interface IFaturaRepository
{
    Task<Fatura?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Fatura>> ListAsync(FaturaFilter filter, CancellationToken cancellationToken = default);
    Task AddAsync(Fatura fatura, CancellationToken cancellationToken = default);
    void Update(Fatura fatura);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
