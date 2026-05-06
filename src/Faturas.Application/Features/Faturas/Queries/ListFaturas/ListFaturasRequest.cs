using Faturas.Application.Common.Mediator;
using Faturas.Application.Common.Results;

namespace Faturas.Application.Features.Faturas.Queries.ListFaturas;

public sealed record ListFaturasRequest(
    string? NomeCliente,
    DateTime? DataInicial,
    DateTime? DataFinal,
    string? Status,
    int Pagina = 1,
    int TamanhoPagina = 10
) : IQuery<Result<ListFaturasPagedResponse>>;
