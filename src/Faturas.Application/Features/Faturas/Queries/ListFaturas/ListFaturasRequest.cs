using Faturas.Application.Common.Mediator;
using Faturas.Application.Common.Results;

namespace Faturas.Application.Features.Faturas.Queries.ListFaturas;

public sealed record ListFaturasRequest(
    string? NomeCliente,
    DateTime? DataInicial,
    DateTime? DataFinal,
    string? Status
) : IQuery<Result<IReadOnlyList<ListFaturasResponse>>>;
