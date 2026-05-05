using MediatR;

namespace Faturas.Application.Common.Mediator;

public interface IQuery<TResponse> : IRequest<TResponse> { }
