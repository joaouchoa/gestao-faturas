using Faturas.Application.Common.Errors;
using FluentValidation;

namespace Faturas.Application.Features.Faturas.Commands.AddItemFatura;

public sealed class AddItemFaturaValidator : AbstractValidator<AddItemFaturaRequest>
{
    public AddItemFaturaValidator()
    {
        RuleFor(x => x.FaturaId)
            .NotEmpty().WithMessage(ApplicationErrorMessages.ItemFatura.FaturaIdObrigatorio);

        RuleFor(x => x.Descricao)
            .NotEmpty().WithMessage(ApplicationErrorMessages.ItemFatura.DescricaoObrigatoria)
            .MinimumLength(3).WithMessage(ApplicationErrorMessages.ItemFatura.DescricaoTamanhoMinimo);

        RuleFor(x => x.Quantidade)
            .GreaterThan(0).WithMessage(ApplicationErrorMessages.ItemFatura.QuantidadeInvalida);

        RuleFor(x => x.ValorUnitario)
            .GreaterThan(0).WithMessage(ApplicationErrorMessages.ItemFatura.ValorUnitarioInvalido);
    }
}
