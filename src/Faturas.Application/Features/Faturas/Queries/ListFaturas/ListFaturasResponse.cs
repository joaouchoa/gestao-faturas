namespace Faturas.Application.Features.Faturas.Queries.ListFaturas;

public sealed record ListFaturasResponse(
    Guid Id,
    string Numero,
    string NomeCliente,
    DateTime DataEmissao,
    string Status,
    decimal ValorTotal,
    int TotalItens
);
