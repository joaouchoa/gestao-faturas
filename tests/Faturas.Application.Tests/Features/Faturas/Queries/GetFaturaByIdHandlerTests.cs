using Faturas.Application.Common.Errors;
using Faturas.Application.Features.Faturas.Queries.GetFaturaById;
using Faturas.Application.Tests.Common.Fakers;
using Faturas.Domain.Faturas;
using Faturas.Domain.Faturas.Repositories;
using FluentAssertions;
using NSubstitute;

namespace Faturas.Application.Tests.Features.Faturas.Queries;

public class GetFaturaByIdHandlerTests
{
    private readonly IFaturaRepository _repository;
    private readonly GetFaturaByIdHandler _handler;

    public GetFaturaByIdHandlerTests()
    {
        _repository = Substitute.For<IFaturaRepository>();
        _handler = new GetFaturaByIdHandler(_repository);
    }

    [Fact]
    public async Task Handle_DeveRetornarSucesso_QuandoFaturaEncontrada()
    {
        // Arrange
        var fatura = new FaturaFaker().Generate();
        fatura.AdicionarItem("Notebook", 1, 500m);
        _repository.GetByIdAsync(fatura.Id, Arg.Any<CancellationToken>()).Returns(fatura);
        var request = new GetFaturaByIdRequest(fatura.Id);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(fatura.Id);
        result.Value.Numero.Should().Be(fatura.Numero.Valor);
        result.Value.NomeCliente.Should().Be(fatura.NomeCliente);
        result.Value.Itens.Should().HaveCount(1);
        result.Value.ValorTotal.Should().Be(500m);
    }

    [Fact]
    public async Task Handle_DeveRetornarFalha_QuandoFaturaNaoEncontrada()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Fatura?)null);
        var request = new GetFaturaByIdRequest(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("NotFound");
        result.Error.Message.Should().Be(ApplicationErrorMessages.Fatura.FaturaNaoEncontrada);
    }
}
