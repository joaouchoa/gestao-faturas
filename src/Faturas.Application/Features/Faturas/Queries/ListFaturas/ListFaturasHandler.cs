using Faturas.Application.Common.Mediator;
using Faturas.Application.Common.Results;
using Faturas.Domain.Faturas;
using Faturas.Domain.Faturas.Repositories;

namespace Faturas.Application.Features.Faturas.Queries.ListFaturas;

public sealed class ListFaturasHandler
    : IQueryHandler<ListFaturasRequest, Result<ListFaturasPagedResponse>>
{
    private readonly IFaturaRepository _repository;

    public ListFaturasHandler(IFaturaRepository repository) => _repository = repository;

    public async Task<Result<ListFaturasPagedResponse>> Handle(
        ListFaturasRequest request,
        CancellationToken cancellationToken)
    {
        StatusFatura? status = null;

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<StatusFatura>(request.Status, ignoreCase: true, out var parsed))
        {
            status = parsed;
        }

        var pagina       = request.Pagina < 1 ? 1 : request.Pagina;
        var tamanhoPagina = request.TamanhoPagina is < 1 or > 100 ? 10 : request.TamanhoPagina;

        var filter = new FaturaFilter(
            request.NomeCliente,
            request.DataInicial,
            request.DataFinal,
            status,
            pagina,
            tamanhoPagina);

        var faturas = await _repository.ListAsync(filter, cancellationToken);
        var total   = await _repository.CountAsync(filter, cancellationToken);

        var itens = faturas
            .Select(f => new ListFaturasResponse(
                f.Id,
                f.Numero.Valor,
                f.NomeCliente,
                f.DataEmissao,
                f.Status.ToString(),
                f.ValorTotal,
                f.Itens.Count))
            .ToList();

        var totalPaginas = (int)Math.Ceiling(total / (double)tamanhoPagina);

        return Result<ListFaturasPagedResponse>.Success(new ListFaturasPagedResponse(
            itens,
            total,
            pagina,
            tamanhoPagina,
            totalPaginas));
    }
}
