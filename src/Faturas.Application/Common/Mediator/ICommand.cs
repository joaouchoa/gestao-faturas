using MediatR;

namespace Faturas.Application.Common.Mediator;

public interface ICommand<TResponse> : IRequest<TResponse> { }
