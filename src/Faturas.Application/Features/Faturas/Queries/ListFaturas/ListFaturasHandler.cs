using Faturas.Application.Common.Mediator;
using Faturas.Application.Common.Results;
using Faturas.Domain.Faturas;
using Faturas.Domain.Faturas.Repositories;

namespace Faturas.Application.Features.Faturas.Queries.ListFaturas;

public sealed class ListFaturasHandler
    : IQueryHandler<ListFaturasRequest, Result<IReadOnlyList<ListFaturasResponse>>>
{
    private readonly IFaturaRepository _repository;

    public ListFaturasHandler(IFaturaRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyList<ListFaturasResponse>>> Handle(
        ListFaturasRequest request,
        CancellationToken cancellationToken)
    {
        StatusFatura? status = null;

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<StatusFatura>(request.Status, ignoreCase: true, out var parsed))
        {
            status = parsed;
        }

        var filter = new FaturaFilter(
            request.NomeCliente,
            request.DataInicial,
            request.DataFinal,
            status);

        var faturas = await _repository.ListAsync(filter, cancellationToken);

        var response = faturas
            .Select(f => new ListFaturasResponse(
                f.Id,
                f.Numero.Valor,
                f.NomeCliente,
                f.DataEmissao,
                f.Status.ToString(),
                f.ValorTotal,
                f.Itens.Count))
            .ToList();

        return Result<IReadOnlyList<ListFaturasResponse>>.Success(response);
    }
}
