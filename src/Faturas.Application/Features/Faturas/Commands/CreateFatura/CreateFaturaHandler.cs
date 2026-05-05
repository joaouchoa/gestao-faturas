using Faturas.Application.Common.Mediator;
using Faturas.Application.Common.Results;
using Faturas.Domain.Faturas;
using Faturas.Domain.Faturas.Repositories;

namespace Faturas.Application.Features.Faturas.Commands.CreateFatura;

public sealed class CreateFaturaHandler
    : ICommandHandler<CreateFaturaRequest, Result<CreateFaturaResponse>>
{
    private readonly IFaturaRepository _repository;

    public CreateFaturaHandler(IFaturaRepository repository) => _repository = repository;

    public async Task<Result<CreateFaturaResponse>> Handle(
        CreateFaturaRequest request,
        CancellationToken cancellationToken)
    {
        var fatura = Fatura.Criar(request.Numero, request.NomeCliente, request.DataEmissao);

        await _repository.AddAsync(fatura, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<CreateFaturaResponse>.Success(new CreateFaturaResponse(
            fatura.Id,
            fatura.Numero.Valor,
            fatura.NomeCliente,
            fatura.DataEmissao,
            fatura.Status.ToString(),
            fatura.ValorTotal));
    }
}
