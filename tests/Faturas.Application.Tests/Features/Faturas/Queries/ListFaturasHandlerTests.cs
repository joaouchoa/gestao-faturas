using Faturas.Application.Features.Faturas.Queries.ListFaturas;
using Faturas.Application.Tests.Common.Fakers;
using Faturas.Domain.Faturas;
using Faturas.Domain.Faturas.Repositories;
using FluentAssertions;
using NSubstitute;

namespace Faturas.Application.Tests.Features.Faturas.Queries;

public class ListFaturasHandlerTests
{
    private readonly IFaturaRepository _repository;
    private readonly ListFaturasHandler _handler;

    public ListFaturasHandlerTests()
    {
        _repository = Substitute.For<IFaturaRepository>();
        _handler = new ListFaturasHandler(_repository);
    }

    [Fact]
    public async Task Handle_DeveRetornarPaginaVazia_QuandoNaoHaFaturas()
    {
        // Arrange
        _repository.ListAsync(Arg.Any<FaturaFilter>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Fatura>)new List<Fatura>());
        _repository.CountAsync(Arg.Any<FaturaFilter>(), Arg.Any<CancellationToken>()).Returns(0);
        var request = new ListFaturasRequest(null, null, null, null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalRegistros.Should().Be(0);
        result.Value.Itens.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_DeveRetornarFaturas_QuandoExistemRegistros()
    {
        // Arrange
        var faturas = new FaturaFaker().Generate(3);
        _repository.ListAsync(Arg.Any<FaturaFilter>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Fatura>)faturas);
        _repository.CountAsync(Arg.Any<FaturaFilter>(), Arg.Any<CancellationToken>()).Returns(3);
        var request = new ListFaturasRequest(null, null, null, null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalRegistros.Should().Be(3);
        result.Value.Itens.Should().HaveCount(3);
        result.Value.TotalPaginas.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DeveUsarValoresPadrao_QuandoPaginacaoInvalida()
    {
        // Arrange
        _repository.ListAsync(Arg.Any<FaturaFilter>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Fatura>)new List<Fatura>());
        _repository.CountAsync(Arg.Any<FaturaFilter>(), Arg.Any<CancellationToken>()).Returns(0);
        var request = new ListFaturasRequest(null, null, null, null, Pagina: 0, TamanhoPagina: 0);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Pagina.Should().Be(1);
        result.Value.TamanhoPagina.Should().Be(10);
    }
}
