using Faturas.Application.Common.Errors;
using Faturas.Application.Features.Faturas.Commands.FecharFatura;
using Faturas.Application.Tests.Common.Fakers;
using Faturas.Domain.Faturas;
using Faturas.Domain.Faturas.Repositories;
using FluentAssertions;
using NSubstitute;

namespace Faturas.Application.Tests.Features.Faturas.Commands;

public class FecharFaturaHandlerTests
{
    private readonly IFaturaRepository _repository;
    private readonly FecharFaturaHandler _handler;

    public FecharFaturaHandlerTests()
    {
        _repository = Substitute.For<IFaturaRepository>();
        _handler = new FecharFaturaHandler(_repository);
    }

    [Fact]
    public async Task Handle_DeveRetornarSucesso_QuandoFaturaEncontrada()
    {
        // Arrange
        var fatura = new FaturaFaker().Generate();
        _repository.GetByIdAsync(fatura.Id, Arg.Any<CancellationToken>()).Returns(fatura);
        var request = new FecharFaturaRequest(fatura.Id);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Fechada");
        _repository.Received(1).Update(fatura);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DeveRetornarFalha_QuandoFaturaNaoEncontrada()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Fatura?)null);
        var request = new FecharFaturaRequest(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("NotFound");
        result.Error.Message.Should().Be(ApplicationErrorMessages.Fatura.FaturaNaoEncontrada);
    }
}
