using Faturas.Application.Common.Errors;
using FluentValidation;

namespace Faturas.Application.Features.Faturas.Commands.UpdateItemFatura;

public sealed class UpdateItemFaturaValidator : AbstractValidator<UpdateItemFaturaRequest>
{
    public UpdateItemFaturaValidator()
    {
        RuleFor(x => x.FaturaId)
            .NotEmpty().WithMessage(ApplicationErrorMessages.ItemFatura.FaturaIdObrigatorio);

        RuleFor(x => x.ItemId)
            .NotEmpty().WithMessage(ApplicationErrorMessages.ItemFatura.ItemIdObrigatorio);

        RuleFor(x => x.Descricao)
            .NotEmpty().WithMessage(ApplicationErrorMessages.ItemFatura.DescricaoObrigatoria)
            .MinimumLength(3).WithMessage(ApplicationErrorMessages.ItemFatura.DescricaoTamanhoMinimo);

        RuleFor(x => x.Quantidade)
            .GreaterThan(0).WithMessage(ApplicationErrorMessages.ItemFatura.QuantidadeInvalida);

        RuleFor(x => x.ValorUnitario)
            .GreaterThan(0).WithMessage(ApplicationErrorMessages.ItemFatura.ValorUnitarioInvalido);
    }
}
