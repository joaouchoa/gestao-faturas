using Faturas.Application.Common.Errors;
using Faturas.Application.Common.Mediator;
using Faturas.Application.Common.Results;
using Faturas.Domain.Faturas.Repositories;

namespace Faturas.Application.Features.Faturas.Queries.GetFaturaById;

public sealed class GetFaturaByIdHandler
    : IQueryHandler<GetFaturaByIdRequest, Result<GetFaturaByIdResponse>>
{
    private readonly IFaturaRepository _repository;

    public GetFaturaByIdHandler(IFaturaRepository repository) => _repository = repository;

    public async Task<Result<GetFaturaByIdResponse>> Handle(
        GetFaturaByIdRequest request,
        CancellationToken cancellationToken)
    {
        var fatura = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (fatura is null)
            return Result<GetFaturaByIdResponse>.Failure(
                Error.NotFound(ApplicationErrorMessages.Fatura.FaturaNaoEncontrada));

        var response = new GetFaturaByIdResponse(
            fatura.Id,
            fatura.Numero.Valor,
            fatura.NomeCliente,
            fatura.DataEmissao,
            fatura.Status.ToString(),
            fatura.ValorTotal,
            fatura.Itens.Select(i => new ItemFaturaResponse(
                i.Id,
                i.Descricao,
                i.Quantidade,
                i.ValorUnitario,
                i.ValorTotalItem,
                i.Justificativa)).ToList());

        return Result<GetFaturaByIdResponse>.Success(response);
    }
}
