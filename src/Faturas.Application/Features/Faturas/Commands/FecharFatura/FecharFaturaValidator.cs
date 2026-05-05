using Faturas.Application.Common.Errors;
using FluentValidation;

namespace Faturas.Application.Features.Faturas.Commands.FecharFatura;

public sealed class FecharFaturaValidator : AbstractValidator<FecharFaturaRequest>
{
    public FecharFaturaValidator()
    {
        RuleFor(x => x.FaturaId)
            .NotEmpty().WithMessage(ApplicationErrorMessages.ItemFatura.FaturaIdObrigatorio);
    }
}
