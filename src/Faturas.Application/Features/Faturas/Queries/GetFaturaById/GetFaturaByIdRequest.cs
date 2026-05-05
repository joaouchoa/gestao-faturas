using Faturas.Application.Common.Mediator;
using Faturas.Application.Common.Results;

namespace Faturas.Application.Features.Faturas.Queries.GetFaturaById;

public sealed record GetFaturaByIdRequest(Guid Id)
    : IQuery<Result<GetFaturaByIdResponse>>;
