namespace Faturas.Application.Features.Faturas.Queries.GetFaturaById;

public sealed record GetFaturaByIdResponse(
    Guid Id,
    string Numero,
    string NomeCliente,
    DateTime DataEmissao,
    string Status,
    decimal ValorTotal,
    IReadOnlyList<ItemFaturaResponse> Itens
);

public sealed record ItemFaturaResponse(
    Guid Id,
    string Descricao,
    int Quantidade,
    decimal ValorUnitario,
    decimal ValorTotalItem,
    string? Justificativa
);
