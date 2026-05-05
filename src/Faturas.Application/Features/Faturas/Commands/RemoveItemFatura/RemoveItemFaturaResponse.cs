namespace Faturas.Application.Features.Faturas.Commands.RemoveItemFatura;

public sealed record RemoveItemFaturaResponse(Guid FaturaId, decimal NovoValorTotal);
