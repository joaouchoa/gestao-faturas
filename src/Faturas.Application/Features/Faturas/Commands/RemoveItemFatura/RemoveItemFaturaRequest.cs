using Faturas.Application.Common.Mediator;
using Faturas.Application.Common.Results;

namespace Faturas.Application.Features.Faturas.Commands.RemoveItemFatura;

public sealed record RemoveItemFaturaRequest(Guid FaturaId, Guid ItemId)
    : ICommand<Result<RemoveItemFaturaResponse>>;
