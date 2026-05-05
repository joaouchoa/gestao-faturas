namespace Faturas.Application.Features.Faturas.Commands.AddItemFatura;

public sealed record AddItemFaturaResponse(
    Guid ItemId,
    Guid FaturaId,
    string Descricao,
    int Quantidade,
    decimal ValorUnitario,
    decimal ValorTotalItem,
    string? Justificativa
);
