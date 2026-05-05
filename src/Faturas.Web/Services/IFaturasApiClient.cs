using Faturas.Web.Models.ViewModels;

namespace Faturas.Web.Services;

public interface IFaturasApiClient
{
    Task<List<FaturaListItemViewModel>> ListAsync(string? cliente, DateTime? dataInicial, DateTime? dataFinal, string? status, CancellationToken ct = default);
    Task<FaturaDetailsViewModel?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(Guid? Id, Dictionary<string, string[]>? Errors)> CreateAsync(CreateFaturaViewModel vm, CancellationToken ct = default);
    Task<string?> AddItemAsync(Guid faturaId, AddItemViewModel vm, CancellationToken ct = default);
    Task<string?> FecharAsync(Guid id, CancellationToken ct = default);
    Task<string?> RemoveItemAsync(Guid faturaId, Guid itemId, CancellationToken ct = default);
}
