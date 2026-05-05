using MediatR;

namespace Faturas.Application.Common.Mediator;

public interface IQueryHandler<TQuery, TResponse>
    : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse> { }
