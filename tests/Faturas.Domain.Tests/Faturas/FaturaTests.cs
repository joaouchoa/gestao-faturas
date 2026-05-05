using Faturas.Domain.Common;
using Faturas.Domain.Faturas;
using Faturas.Domain.Faturas.Errors;
using Faturas.Domain.Tests.Faturas.Builders;
using FluentAssertions;

namespace Faturas.Domain.Tests.Faturas;

public class FaturaTests
{
    // ── Criação ──────────────────────────────────────────────────────────────

    [Fact]
    public void Criar_DeveCriarFatura_QuandoDadosValidos()
    {
        // Arrange
        var numero = "FAT-000001";
        var nomeCliente = "João da Silva";
        var dataEmissao = DateTime.UtcNow;

        // Act
        var fatura = Fatura.Criar(numero, nomeCliente, dataEmissao);

        // Assert
        fatura.Numero.Valor.Should().Be(numero);
        fatura.NomeCliente.Should().Be(nomeCliente);
        fatura.Status.Should().Be(StatusFatura.Aberta);
        fatura.ValorTotal.Should().Be(0m);
        fatura.Itens.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_DeveLancar_QuandoNomeClienteVazio(string nomeCliente)
    {
        // Act
        Action act = () => Fatura.Criar("FAT-000001", nomeCliente, DateTime.UtcNow);

        // Assert
        act.Should().Throw<DomainException>()
           .WithMessage(FaturaErrors.NomeClienteObrigatorio);
    }

    [Fact]
    public void Criar_DeveLancar_QuandoNumeroFormatoInvalido()
    {
        // Act
        Action act = () => Fatura.Criar("INVALIDO", "Cliente", DateTime.UtcNow);

        // Assert
        act.Should().Throw<DomainException>()
           .WithMessage(FaturaErrors.NumeroFormatoInvalido);
    }

    // ── Adicionar Item ────────────────────────────────────────────────────────

    [Fact]
    public void AdicionarItem_DeveAdicionarItem_QuandoDadosValidos()
    {
        // Arrange
        var fatura = new FaturaFaker().Generate();

        // Act
        fatura.AdicionarItem("Notebook", 1, 500m);

        // Assert
        fatura.Itens.Should().HaveCount(1);
        fatura.ValorTotal.Should().Be(500m);
    }

    [Fact]
    public void AdicionarItem_DeveRecalcularValorTotal()
    {
        // Arrange
        var fatura = new FaturaFaker().Generate();

        // Act
        fatura.AdicionarItem("Monitor", 2, 300m);
        fatura.AdicionarItem("Teclado", 1, 150m);

        // Assert
        fatura.ValorTotal.Should().Be(750m);
    }

    [Fact]
    public void AdicionarItem_DeveLancar_QuandoFaturaFechada()
    {
        // Arrange
        var fatura = new FaturaFaker().Generate();
        fatura.Fechar();

        // Act
        Action act = () => fatura.AdicionarItem("Produto", 1, 10m);

        // Assert
        act.Should().Throw<DomainException>()
           .WithMessage(FaturaErrors.FaturaJaFechada);
    }

    [Fact]
    public void AdicionarItem_DeveLancar_QuandoValorMaiorQue1000ESemJustificativa()
    {
        // Arrange
        var fatura = new FaturaFaker().Generate();

        // Act
        Action act = () => fatura.AdicionarItem("Servidor", 2, 600m, justificativa: null);

        // Assert
        act.Should().Throw<DomainException>()
           .WithMessage(FaturaErrors.JustificativaObrigatoriaAcimaDe1000);
    }

    [Fact]
    public void AdicionarItem_DeveAdicionar_QuandoValorMaiorQue1000ComJustificativa()
    {
        // Arrange
        var fatura = new FaturaFaker().Generate();

        // Act
        fatura.AdicionarItem("Servidor", 2, 600m, justificativa: "Aprovado pelo gestor");

        // Assert
        fatura.Itens.Should().HaveCount(1);
        fatura.ValorTotal.Should().Be(1200m);
    }

    // ── Fechar ────────────────────────────────────────────────────────────────

    [Fact]
    public void Fechar_DeveAlterarStatusParaFechada()
    {
        // Arrange
        var fatura = new FaturaFaker().Generate();

        // Act
        fatura.Fechar();

        // Assert
        fatura.Status.Should().Be(StatusFatura.Fechada);
    }

    [Fact]
    public void Fechar_DeveLancar_QuandoJaFechada()
    {
        // Arrange
        var fatura = new FaturaFaker().Generate();
        fatura.Fechar();

        // Act
        Action act = () => fatura.Fechar();

        // Assert
        act.Should().Throw<DomainException>()
           .WithMessage(FaturaErrors.FaturaJaFechada);
    }

    // ── Remover / Atualizar Item ──────────────────────────────────────────────

    [Fact]
    public void RemoverItem_DeveLancar_QuandoFaturaFechada()
    {
        // Arrange
        var fatura = new FaturaFaker().Generate();
        var item = fatura.AdicionarItem("Produto", 1, 10m);
        fatura.Fechar();

        // Act
        Action act = () => fatura.RemoverItem(item.Id);

        // Assert
        act.Should().Throw<DomainException>()
           .WithMessage(FaturaErrors.FaturaJaFechada);
    }

    [Fact]
    public void AtualizarItem_DeveLancar_QuandoFaturaFechada()
    {
        // Arrange
        var fatura = new FaturaFaker().Generate();
        var item = fatura.AdicionarItem("Produto", 1, 10m);
        fatura.Fechar();

        // Act
        Action act = () => fatura.AtualizarItem(item.Id, "Novo", 2, 20m);

        // Assert
        act.Should().Throw<DomainException>()
           .WithMessage(FaturaErrors.FaturaJaFechada);
    }
}
