using Faturas.Application.Common.Errors;
using Faturas.Application.Common.Mediator;
using Faturas.Application.Common.Results;
using Faturas.Domain.Faturas.Repositories;

namespace Faturas.Application.Features.Faturas.Commands.FecharFatura;

public sealed class FecharFaturaHandler
    : ICommandHandler<FecharFaturaRequest, Result<FecharFaturaResponse>>
{
    private readonly IFaturaRepository _repository;

    public FecharFaturaHandler(IFaturaRepository repository) => _repository = repository;

    public async Task<Result<FecharFaturaResponse>> Handle(
        FecharFaturaRequest request,
        CancellationToken cancellationToken)
    {
        var fatura = await _repository.GetByIdAsync(request.FaturaId, cancellationToken);

        if (fatura is null)
            return Result<FecharFaturaResponse>.Failure(
                Error.NotFound(ApplicationErrorMessages.Fatura.FaturaNaoEncontrada));

        fatura.Fechar();

        _repository.Update(fatura);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<FecharFaturaResponse>.Success(
            new FecharFaturaResponse(fatura.Id, fatura.Status.ToString()));
    }
}
