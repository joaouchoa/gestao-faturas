using Faturas.Application.Common.Mediator;
using Faturas.Application.Common.Results;

namespace Faturas.Application.Features.Faturas.Commands.UpdateItemFatura;

public sealed record UpdateItemFaturaRequest(
    Guid FaturaId,
    Guid ItemId,
    string Descricao,
    int Quantidade,
    decimal ValorUnitario,
    string? Justificativa
) : ICommand<Result<UpdateItemFaturaResponse>>;
