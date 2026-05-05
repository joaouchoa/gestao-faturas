using Faturas.Application.Common.Errors;
using Faturas.Application.Common.Mediator;
using Faturas.Application.Common.Results;
using Faturas.Domain.Faturas.Repositories;

namespace Faturas.Application.Features.Faturas.Commands.AddItemFatura;

public sealed class AddItemFaturaHandler
    : ICommandHandler<AddItemFaturaRequest, Result<AddItemFaturaResponse>>
{
    private readonly IFaturaRepository _repository;

    public AddItemFaturaHandler(IFaturaRepository repository) => _repository = repository;

    public async Task<Result<AddItemFaturaResponse>> Handle(
        AddItemFaturaRequest request,
        CancellationToken cancellationToken)
    {
        var fatura = await _repository.GetByIdAsync(request.FaturaId, cancellationToken);

        if (fatura is null)
            return Result<AddItemFaturaResponse>.Failure(
                Error.NotFound(ApplicationErrorMessages.ItemFatura.FaturaIdObrigatorio));

        var item = fatura.AdicionarItem(
            request.Descricao,
            request.Quantidade,
            request.ValorUnitario,
            request.Justificativa);

        await _repository.SaveChangesAsync(cancellationToken);

        return Result<AddItemFaturaResponse>.Success(new AddItemFaturaResponse(
            item.Id,
            fatura.Id,
            item.Descricao,
            item.Quantidade,
            item.ValorUnitario,
            item.ValorTotalItem,
            item.Justificativa));
    }
}
