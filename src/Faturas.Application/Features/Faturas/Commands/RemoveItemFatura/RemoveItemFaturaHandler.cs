using Faturas.Application.Common.Errors;
using Faturas.Application.Common.Mediator;
using Faturas.Application.Common.Results;
using Faturas.Domain.Faturas.Repositories;

namespace Faturas.Application.Features.Faturas.Commands.RemoveItemFatura;

public sealed class RemoveItemFaturaHandler
    : ICommandHandler<RemoveItemFaturaRequest, Result<RemoveItemFaturaResponse>>
{
    private readonly IFaturaRepository _repository;

    public RemoveItemFaturaHandler(IFaturaRepository repository) => _repository = repository;

    public async Task<Result<RemoveItemFaturaResponse>> Handle(
        RemoveItemFaturaRequest request,
        CancellationToken cancellationToken)
    {
        var fatura = await _repository.GetByIdAsync(request.FaturaId, cancellationToken);

        if (fatura is null)
            return Result<RemoveItemFaturaResponse>.Failure(
                Error.NotFound(ApplicationErrorMessages.Fatura.FaturaNaoEncontrada));

        fatura.RemoverItem(request.ItemId);

        _repository.Update(fatura);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<RemoveItemFaturaResponse>.Success(
            new RemoveItemFaturaResponse(fatura.Id, fatura.ValorTotal));
    }
}
