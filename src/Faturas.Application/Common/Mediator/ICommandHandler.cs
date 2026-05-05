using MediatR;

namespace Faturas.Application.Common.Mediator;

public interface ICommandHandler<TCommand, TResponse>
    : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse> { }
