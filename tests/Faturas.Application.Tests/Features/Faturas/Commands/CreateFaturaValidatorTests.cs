using Faturas.Application.Common.Errors;
using Faturas.Application.Features.Faturas.Commands.CreateFatura;
using FluentAssertions;

namespace Faturas.Application.Tests.Features.Faturas.Commands;

public class CreateFaturaValidatorTests
{
    private readonly CreateFaturaValidator _validator = new();

    [Fact]
    public async Task Validar_DevePassar_QuandoDadosValidos()
    {
        var request = new CreateFaturaRequest("FAT-0001", "João Silva", DateTime.UtcNow);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validar_DeveFalhar_QuandoNumeroVazio(string numero)
    {
        var request = new CreateFaturaRequest(numero, "João Silva", DateTime.UtcNow);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == ApplicationErrorMessages.Fatura.NumeroObrigatorio);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validar_DeveFalhar_QuandoNomeClienteVazio(string nomeCliente)
    {
        var request = new CreateFaturaRequest("FAT-0001", nomeCliente, DateTime.UtcNow);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == ApplicationErrorMessages.Fatura.NomeClienteObrigatorio);
    }

    [Fact]
    public async Task Validar_DeveFalhar_QuandoNomeClienteExcedeTamanhoMaximo()
    {
        var nomeGrande = new string('A', 151);
        var request = new CreateFaturaRequest("FAT-0001", nomeGrande, DateTime.UtcNow);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == ApplicationErrorMessages.Fatura.NomeClienteTamanhoMaximo);
    }

    [Fact]
    public async Task Validar_DeveFalhar_QuandoDataEmissaoVazia()
    {
        var request = new CreateFaturaRequest("FAT-0001", "João Silva", default);
        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == ApplicationErrorMessages.Fatura.DataEmissaoObrigatoria);
    }
}
