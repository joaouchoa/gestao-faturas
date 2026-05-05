using Faturas.Domain.Common;
using Faturas.Domain.Faturas;
using Faturas.Domain.Faturas.Errors;
using Faturas.Domain.Tests.Faturas.Builders;
using FluentAssertions;

namespace Faturas.Domain.Tests.Faturas;

public class ItemFaturaTests
{
    [Fact]
    public void Item_DeveLancar_QuandoDescricaoVazia()
    {
        // Arrange
        var fatura = new FaturaFaker().Generate();

        // Act
        Action act = () => fatura.AdicionarItem("", 1, 10m);

        // Assert
        act.Should().Throw<DomainException>()
           .WithMessage(FaturaErrors.DescricaoObrigatoria);
    }

    [Fact]
    public void Item_DeveLancar_QuandoDescricaoMenorQueMinimo()
    {
        // Arrange
        var fatura = new FaturaFaker().Generate();

        // Act
        Action act = () => fatura.AdicionarItem("AB", 1, 10m);

        // Assert
        act.Should().Throw<DomainException>()
           .WithMessage(FaturaErrors.DescricaoTamanhoMinimo);
    }

    [Fact]
    public void Item_DeveLancar_QuandoQuantidadeZero()
    {
        // Arrange
        var fatura = new FaturaFaker().Generate();

        // Act
        Action act = () => fatura.AdicionarItem("Produto", 0, 10m);

        // Assert
        act.Should().Throw<DomainException>()
           .WithMessage(FaturaErrors.QuantidadeInvalida);
    }

    [Fact]
    public void Item_DeveLancar_QuandoValorUnitarioZero()
    {
        // Arrange
        var fatura = new FaturaFaker().Generate();

        // Act
        Action act = () => fatura.AdicionarItem("Produto", 1, 0m);

        // Assert
        act.Should().Throw<DomainException>()
           .WithMessage(FaturaErrors.ValorUnitarioInvalido);
    }

    [Fact]
    public void Item_DeveCalcularValorTotalItem_Corretamente()
    {
        // Arrange
        var fatura = new FaturaFaker().Generate();

        // Act
        var item = fatura.AdicionarItem("Produto", 3, 150m);

        // Assert
        item.ValorTotalItem.Should().Be(450m);
    }
}
