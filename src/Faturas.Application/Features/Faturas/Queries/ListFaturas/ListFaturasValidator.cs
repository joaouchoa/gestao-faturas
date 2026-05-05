using Faturas.Application.Common.Errors;
using FluentValidation;

namespace Faturas.Application.Features.Faturas.Queries.ListFaturas;

public sealed class ListFaturasValidator : AbstractValidator<ListFaturasRequest>
{
    public ListFaturasValidator()
    {
        When(x => x.DataInicial.HasValue && x.DataFinal.HasValue, () =>
        {
            RuleFor(x => x.DataInicial)
                .LessThanOrEqualTo(x => x.DataFinal)
                .WithMessage(ApplicationErrorMessages.ListFaturas.PeriodoInvalido);
        });
    }
}
