using Faturas.Application.Common.Errors;
using Faturas.Application.Features.Faturas.Commands.AddItemFatura;
using Faturas.Application.Tests.Common.Fakers;
using Faturas.Domain.Faturas;
using Faturas.Domain.Faturas.Repositories;
using FluentAssertions;
using NSubstitute;

namespace Faturas.Application.Tests.Features.Faturas.Commands;

public class AddItemFaturaHandlerTests
{
    private readonly IFaturaRepository _repository;
    private readonly AddItemFaturaHandler _handler;

    public AddItemFaturaHandlerTests()
    {
        _repository = Substitute.For<IFaturaRepository>();
        _handler = new AddItemFaturaHandler(_repository);
    }

    [Fact]
    public async Task Handle_DeveRetornarSucesso_QuandoFaturaExiste()
    {
        // Arrange
        var fatura = new FaturaFaker().Generate();
        _repository.GetByIdAsync(fatura.Id, Arg.Any<CancellationToken>()).Returns(fatura);
        var request = new AddItemFaturaRequest(fatura.Id, "Monitor", 2, 300m, null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Descricao.Should().Be("Monitor");
        result.Value.Quantidade.Should().Be(2);
        result.Value.ValorTotalItem.Should().Be(600m);
        _repository.Received(1).AddItem(Arg.Any<ItemFatura>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DeveRetornarFalha_QuandoFaturaNaoEncontrada()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Fatura?)null);
        var request = new AddItemFaturaRequest(Guid.NewGuid(), "Monitor", 1, 300m, null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("NotFound");
    }
}
