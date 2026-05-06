using Faturas.Application.Features.Faturas.Commands.CreateFatura;
using Faturas.Domain.Faturas;
using Faturas.Domain.Faturas.Repositories;
using FluentAssertions;
using NSubstitute;

namespace Faturas.Application.Tests.Features.Faturas.Commands;

public class CreateFaturaHandlerTests
{
    private readonly IFaturaRepository _repository;
    private readonly CreateFaturaHandler _handler;

    public CreateFaturaHandlerTests()
    {
        _repository = Substitute.For<IFaturaRepository>();
        _handler = new CreateFaturaHandler(_repository);
    }

    [Fact]
    public async Task Handle_DeveRetornarSucesso_QuandoDadosValidos()
    {
        // Arrange
        var request = new CreateFaturaRequest("FAT-0001", "João Silva", DateTime.UtcNow);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Numero.Should().Be(request.Numero);
        result.Value.NomeCliente.Should().Be(request.NomeCliente);
        result.Value.Status.Should().Be("Aberta");
        result.Value.ValorTotal.Should().Be(0m);
        await _repository.Received(1).AddAsync(Arg.Any<Fatura>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
