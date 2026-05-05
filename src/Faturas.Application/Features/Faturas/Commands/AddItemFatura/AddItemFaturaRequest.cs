using Faturas.Application.Common.Mediator;
using Faturas.Application.Common.Results;

namespace Faturas.Application.Features.Faturas.Commands.AddItemFatura;

public sealed record AddItemFaturaRequest(
    Guid FaturaId,
    string Descricao,
    int Quantidade,
    decimal ValorUnitario,
    string? Justificativa
) : ICommand<Result<AddItemFaturaResponse>>;
