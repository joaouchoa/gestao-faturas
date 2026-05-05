using Faturas.Application.Common.Mediator;
using Faturas.Application.Common.Results;

namespace Faturas.Application.Features.Faturas.Commands.CreateFatura;

public sealed record CreateFaturaRequest(
    string Numero,
    string NomeCliente,
    DateTime DataEmissao
) : ICommand<Result<CreateFaturaResponse>>;
