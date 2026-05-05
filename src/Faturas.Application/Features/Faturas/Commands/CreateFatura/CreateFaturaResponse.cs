namespace Faturas.Application.Features.Faturas.Commands.CreateFatura;

public sealed record CreateFaturaResponse(
    Guid Id,
    string Numero,
    string NomeCliente,
    DateTime DataEmissao,
    string Status,
    decimal ValorTotal
);
