using Faturas.Application.Common.Errors;
using FluentValidation;

namespace Faturas.Application.Features.Faturas.Commands.RemoveItemFatura;

public sealed class RemoveItemFaturaValidator : AbstractValidator<RemoveItemFaturaRequest>
{
    public RemoveItemFaturaValidator()
    {
        RuleFor(x => x.FaturaId)
            .NotEmpty().WithMessage(ApplicationErrorMessages.ItemFatura.FaturaIdObrigatorio);

        RuleFor(x => x.ItemId)
            .NotEmpty().WithMessage(ApplicationErrorMessages.ItemFatura.ItemIdObrigatorio);
    }
}
