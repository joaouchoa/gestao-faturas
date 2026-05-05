using Faturas.Application.Common.Mediator;
using Faturas.Application.Common.Results;

namespace Faturas.Application.Features.Faturas.Commands.FecharFatura;

public sealed record FecharFaturaRequest(Guid FaturaId)
    : ICommand<Result<FecharFaturaResponse>>;
