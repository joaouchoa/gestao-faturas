namespace Faturas.Application.Features.Faturas.Commands.UpdateItemFatura;

public sealed record UpdateItemFaturaResponse(
    Guid ItemId,
    string Descricao,
    int Quantidade,
    decimal ValorUnitario,
    decimal ValorTotalItem,
    string? Justificativa
);
