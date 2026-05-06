using Faturas.Application.Common.Errors;
using Faturas.Application.Features.Faturas.Queries.ListFaturas;
using FluentAssertions;

namespace Faturas.Application.Tests.Features.Faturas.Queries;

public class ListFaturasValidatorTests
{
    private readonly ListFaturasValidator _validator = new();

    [Fact]
    public async Task Validar_DevePassar_QuandoSemFiltros()
    {
        var request = new ListFaturasRequest(null, null, null, null);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validar_DevePassar_QuandoPeriodoValido()
    {
        var request = new ListFaturasRequest(null, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow, null);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validar_DeveFalhar_QuandoDataInicialMaiorQueDataFinal()
    {
        var request = new ListFaturasRequest(null, DateTime.UtcNow, DateTime.UtcNow.AddDays(-7), null);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == ApplicationErrorMessages.ListFaturas.PeriodoInvalido);
    }
}
