using Faturas.Integration.Tests.Common.Dtos;
using Faturas.Integration.Tests.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Faturas.Integration.Tests.Features.Faturas;

[Collection(FaturasCollection.Name)]
public class FaturasEndpointTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public FaturasEndpointTests(IntegrationWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── POST /api/faturas ─────────────────────────────────────────────────────

    [Fact]
    public async Task CriarFatura_DeveRetornar201_QuandoDadosValidos()
    {
        var numero = GerarNumero();
        var body = new
        {
            Numero = numero,
            NomeCliente = "João da Silva",
            DataEmissao = DateTime.UtcNow
        };

        var response = await _client.PostAsJsonAsync("/api/faturas", body);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var fatura = await response.Content.ReadFromJsonAsync<FaturaDto>(JsonOptions);
        fatura!.Id.Should().NotBeEmpty();
        fatura.Numero.Should().Be(numero);
        fatura.Status.Should().Be("Aberta");
        fatura.ValorTotal.Should().Be(0m);
    }

    [Fact]
    public async Task CriarFatura_DeveRetornar400_QuandoNumeroVazio()
    {
        var body = new
        {
            Numero = "",
            NomeCliente = "João da Silva",
            DataEmissao = DateTime.UtcNow
        };

        var response = await _client.PostAsJsonAsync("/api/faturas", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CriarFatura_DeveRetornar400_QuandoNomeClienteVazio()
    {
        var body = new
        {
            Numero = GerarNumero(),
            NomeCliente = "",
            DataEmissao = DateTime.UtcNow
        };

        var response = await _client.PostAsJsonAsync("/api/faturas", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── GET /api/faturas/{id} ─────────────────────────────────────────────────

    [Fact]
    public async Task GetFatura_DeveRetornar200_QuandoFaturaExiste()
    {
        var faturaId = await CriarFaturaAsync();

        var response = await _client.GetAsync($"/api/faturas/{faturaId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var fatura = await response.Content.ReadFromJsonAsync<FaturaDetailDto>(JsonOptions);
        fatura!.Id.Should().Be(faturaId);
        fatura.Itens.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFatura_DeveRetornar404_QuandoFaturaNaoEncontrada()
    {
        var idInexistente = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/faturas/{idInexistente}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/faturas ──────────────────────────────────────────────────────

    [Fact]
    public async Task ListarFaturas_DeveRetornar200_ComEstruturaDePaginacao()
    {
        // Arrange — garantir que ao menos uma fatura existe
        await CriarFaturaAsync();

        var response = await _client.GetAsync("/api/faturas?pagina=1&tamanhoPagina=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var paged = await response.Content.ReadFromJsonAsync<PagedFaturasDto>(JsonOptions);
        paged!.Pagina.Should().Be(1);
        paged.TamanhoPagina.Should().Be(10);
        paged.TotalRegistros.Should().BeGreaterThan(0);
        paged.TotalPaginas.Should().BeGreaterThan(0);
        paged.Itens.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ListarFaturas_DeveRetornar400_QuandoPeriodoInvalido()
    {
        // dataInicial > dataFinal → violação de validação
        var dataInicial = DateTime.UtcNow.ToString("O");
        var dataFinal   = DateTime.UtcNow.AddDays(-7).ToString("O");

        var response = await _client.GetAsync(
            $"/api/faturas?dataInicial={Uri.EscapeDataString(dataInicial)}&dataFinal={Uri.EscapeDataString(dataFinal)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── POST /api/faturas/{id}/itens ─────────────────────────────────────────

    [Fact]
    public async Task AddItem_DeveRetornar404_QuandoFaturaNaoEncontrada()
    {
        var idInexistente = Guid.NewGuid();
        var body = new
        {
            Descricao = "Produto",
            Quantidade = 1,
            ValorUnitario = 100m,
            Justificativa = (string?)null
        };

        var response = await _client.PostAsJsonAsync($"/api/faturas/{idInexistente}/itens", body);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddItem_DeveRetornar400_QuandoDescricaoMenorQueMinimo()
    {
        var faturaId = await CriarFaturaAsync();
        var body = new
        {
            Descricao = "AB",   // mínimo é 3 chars
            Quantidade = 1,
            ValorUnitario = 100m,
            Justificativa = (string?)null
        };

        var response = await _client.PostAsJsonAsync($"/api/faturas/{faturaId}/itens", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── PUT /api/faturas/{id}/fechar ──────────────────────────────────────────

    [Fact]
    public async Task Fechar_DeveRetornar404_QuandoFaturaNaoEncontrada()
    {
        var idInexistente = Guid.NewGuid();

        var response = await _client.PutAsync(
            $"/api/faturas/{idInexistente}/fechar",
            new StringContent(string.Empty));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Fechar_DeveRetornar200_QuandoFaturaAberta()
    {
        var faturaId = await CriarFaturaAsync();

        var response = await _client.PutAsync(
            $"/api/faturas/{faturaId}/fechar",
            new StringContent(string.Empty));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultado = await response.Content.ReadFromJsonAsync<FecharDto>(JsonOptions);
        resultado!.Status.Should().Be("Fechada");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> CriarFaturaAsync()
    {
        var body = new
        {
            Numero = GerarNumero(),
            NomeCliente = "Cliente Teste",
            DataEmissao = DateTime.UtcNow
        };

        var response = await _client.PostAsJsonAsync("/api/faturas", body);
        response.EnsureSuccessStatusCode();

        var fatura = await response.Content.ReadFromJsonAsync<FaturaDto>(JsonOptions);
        return fatura!.Id;
    }

    private static string GerarNumero() =>
        $"INT-{Guid.NewGuid().ToString()[..8].ToUpper()}";
}
