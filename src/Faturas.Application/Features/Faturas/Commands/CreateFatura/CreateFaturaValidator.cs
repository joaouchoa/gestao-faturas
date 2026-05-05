using Faturas.Application.Common.Errors;
using FluentValidation;

namespace Faturas.Application.Features.Faturas.Commands.CreateFatura;

public sealed class CreateFaturaValidator : AbstractValidator<CreateFaturaRequest>
{
    public CreateFaturaValidator()
    {
        RuleFor(x => x.Numero)
            .NotEmpty().WithMessage(ApplicationErrorMessages.Fatura.NumeroObrigatorio);

        RuleFor(x => x.NomeCliente)
            .NotEmpty().WithMessage(ApplicationErrorMessages.Fatura.NomeClienteObrigatorio)
            .MaximumLength(150).WithMessage(ApplicationErrorMessages.Fatura.NomeClienteTamanhoMaximo);

        RuleFor(x => x.DataEmissao)
            .NotEmpty().WithMessage(ApplicationErrorMessages.Fatura.DataEmissaoObrigatoria);
    }
}
