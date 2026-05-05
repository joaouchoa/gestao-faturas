using Faturas.Application.Common.Errors;
using Faturas.Application.Common.Mediator;
using Faturas.Application.Common.Results;
using Faturas.Domain.Faturas.Repositories;

namespace Faturas.Application.Features.Faturas.Commands.UpdateItemFatura;

public sealed class UpdateItemFaturaHandler
    : ICommandHandler<UpdateItemFaturaRequest, Result<UpdateItemFaturaResponse>>
{
    private readonly IFaturaRepository _repository;

    public UpdateItemFaturaHandler(IFaturaRepository repository) => _repository = repository;

    public async Task<Result<UpdateItemFaturaResponse>> Handle(
        UpdateItemFaturaRequest request,
        CancellationToken cancellationToken)
    {
        var fatura = await _repository.GetByIdAsync(request.FaturaId, cancellationToken);

        if (fatura is null)
            return Result<UpdateItemFaturaResponse>.Failure(
                Error.NotFound(ApplicationErrorMessages.Fatura.FaturaNaoEncontrada));

        fatura.AtualizarItem(
            request.ItemId,
            request.Descricao,
            request.Quantidade,
            request.ValorUnitario,
            request.Justificativa);

        _repository.Update(fatura);
        await _repository.SaveChangesAsync(cancellationToken);

        var item = fatura.Itens.First(i => i.Id == request.ItemId);

        return Result<UpdateItemFaturaResponse>.Success(new UpdateItemFaturaResponse(
            item.Id,
            item.Descricao,
            item.Quantidade,
            item.ValorUnitario,
            item.ValorTotalItem,
            item.Justificativa));
    }
}
