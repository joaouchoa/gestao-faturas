using Faturas.Application.Common.Errors;
using Faturas.Application.Features.Faturas.Commands.AddItemFatura;
using FluentAssertions;

namespace Faturas.Application.Tests.Features.Faturas.Commands;

public class AddItemFaturaValidatorTests
{
    private readonly AddItemFaturaValidator _validator = new();

    [Fact]
    public async Task Validar_DevePassar_QuandoDadosValidos()
    {
        var request = new AddItemFaturaRequest(Guid.NewGuid(), "Monitor", 1, 500m, null);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validar_DeveFalhar_QuandoFaturaIdVazio()
    {
        var request = new AddItemFaturaRequest(Guid.Empty, "Monitor", 1, 500m, null);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == ApplicationErrorMessages.ItemFatura.FaturaIdObrigatorio);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validar_DeveFalhar_QuandoDescricaoVazia(string descricao)
    {
        var request = new AddItemFaturaRequest(Guid.NewGuid(), descricao, 1, 500m, null);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == ApplicationErrorMessages.ItemFatura.DescricaoObrigatoria);
    }

    [Fact]
    public async Task Validar_DeveFalhar_QuandoDescricaoMenorQueMinimo()
    {
        var request = new AddItemFaturaRequest(Guid.NewGuid(), "AB", 1, 500m, null);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == ApplicationErrorMessages.ItemFatura.DescricaoTamanhoMinimo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validar_DeveFalhar_QuandoQuantidadeInvalida(int quantidade)
    {
        var request = new AddItemFaturaRequest(Guid.NewGuid(), "Monitor", quantidade, 500m, null);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == ApplicationErrorMessages.ItemFatura.QuantidadeInvalida);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validar_DeveFalhar_QuandoValorUnitarioInvalido(int valorUnitario)
    {
        var request = new AddItemFaturaRequest(Guid.NewGuid(), "Monitor", 1, valorUnitario, null);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == ApplicationErrorMessages.ItemFatura.ValorUnitarioInvalido);
    }
}
