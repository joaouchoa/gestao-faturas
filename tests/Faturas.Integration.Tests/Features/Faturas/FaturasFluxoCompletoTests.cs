using Faturas.Integration.Tests.Common.Dtos;
using Faturas.Integration.Tests.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Faturas.Integration.Tests.Features.Faturas;

[Collection(FaturasCollection.Name)]
public class FaturasFluxoCompletoTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public FaturasFluxoCompletoTests(IntegrationWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Fluxo Principal ───────────────────────────────────────────────────────

    [Fact]
    public async Task FluxoCompleto_CriarAdicionarItemFechar_DeveExecutarComSucesso()
    {
        var numero = GerarNumero();

        // ── 1. Criar fatura ───────────────────────────────────────────────
        var criarBody = new
        {
            Numero = numero,
            NomeCliente = "Empresa Integração Ltda",
            DataEmissao = DateTime.UtcNow
        };

        var criarResponse = await _client.PostAsJsonAsync("/api/faturas", criarBody);

        criarResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        criarResponse.Headers.Location.Should().NotBeNull();

        var fatura = await criarResponse.Content.ReadFromJsonAsync<FaturaDto>(JsonOptions);
        fatura!.Numero.Should().Be(numero);
        fatura.NomeCliente.Should().Be("Empresa Integração Ltda");
        fatura.Status.Should().Be("Aberta");
        fatura.ValorTotal.Should().Be(0m);

        // ── 2. Adicionar item válido (< R$ 1.000) ─────────────────────────
        var addItemBody = new
        {
            Descricao = "Notebook Dell XPS",
            Quantidade = 1,
            ValorUnitario = 800m,
            Justificativa = (string?)null
        };

        var addItemResponse = await _client.PostAsJsonAsync($"/api/faturas/{fatura.Id}/itens", addItemBody);

        addItemResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var item = await addItemResponse.Content.ReadFromJsonAsync<ItemAddedDto>(JsonOptions);
        item!.Descricao.Should().Be("Notebook Dell XPS");
        item.Quantidade.Should().Be(1);
        item.ValorTotalItem.Should().Be(800m);

        // ── 3. Adicionar segundo item com justificativa (> R$ 1.000) ──────
        var addItemCaroBody = new
        {
            Descricao = "Servidor HP ProLiant",
            Quantidade = 2,
            ValorUnitario = 3500m,
            Justificativa = "Aprovado pelo gestor financeiro — ordem de compra #2024-001"
        };

        var addItemCaroResponse = await _client.PostAsJsonAsync($"/api/faturas/{fatura.Id}/itens", addItemCaroBody);

        addItemCaroResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var itemCaro = await addItemCaroResponse.Content.ReadFromJsonAsync<ItemAddedDto>(JsonOptions);
        itemCaro!.ValorTotalItem.Should().Be(7000m);   // 2 × 3500
        itemCaro.Justificativa.Should().NotBeNullOrEmpty();

        // ── 4. Buscar fatura e verificar itens e total ────────────────────
        var getResponse = await _client.GetAsync($"/api/faturas/{fatura.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var faturaDetail = await getResponse.Content.ReadFromJsonAsync<FaturaDetailDto>(JsonOptions);
        faturaDetail!.Status.Should().Be("Aberta");
        faturaDetail.ValorTotal.Should().Be(7800m);   // 800 + 7000
        faturaDetail.Itens.Should().HaveCount(2);

        // ── 5. Fechar fatura ──────────────────────────────────────────────
        var fecharResponse = await _client.PutAsync(
            $"/api/faturas/{fatura.Id}/fechar",
            new StringContent(string.Empty));

        fecharResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var faturaFechada = await fecharResponse.Content.ReadFromJsonAsync<FecharDto>(JsonOptions);
        faturaFechada!.Status.Should().Be("Fechada");

        // ── 6. Verificar status após fechar (GET) ─────────────────────────
        var getAposFecharResponse = await _client.GetAsync($"/api/faturas/{fatura.Id}");
        var faturaAposFechar = await getAposFecharResponse.Content.ReadFromJsonAsync<FaturaDetailDto>(JsonOptions);
        faturaAposFechar!.Status.Should().Be("Fechada");

        // ── 7. Tentar adicionar item em fatura fechada → RN-5/6 ──────────
        var addItemFechadaBody = new
        {
            Descricao = "Mouse Logitech",
            Quantidade = 1,
            ValorUnitario = 50m,
            Justificativa = (string?)null
        };

        var addItemFechadaResponse = await _client.PostAsJsonAsync(
            $"/api/faturas/{fatura.Id}/itens",
            addItemFechadaBody);

        addItemFechadaResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── Regra de Negócio: Item > R$ 1.000 sem justificativa ───────────────────

    [Fact]
    public async Task AddItem_DeveRetornar422_QuandoValorAcimaDe1000SemJustificativa()
    {
        // Arrange — criar fatura
        var faturaId = await CriarFaturaAsync();

        var addItemBody = new
        {
            Descricao = "Servidor",
            Quantidade = 2,
            ValorUnitario = 600m,   // total = 1200 > 1000
            Justificativa = (string?)null
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/faturas/{faturaId}/itens", addItemBody);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── Regra de Negócio: Item > R$ 1.000 com justificativa ──────────────────

    [Fact]
    public async Task AddItem_DeveRetornar201_QuandoValorAcimaDe1000ComJustificativa()
    {
        // Arrange
        var faturaId = await CriarFaturaAsync();

        var addItemBody = new
        {
            Descricao = "Servidor",
            Quantidade = 2,
            ValorUnitario = 600m,
            Justificativa = "Aprovado pelo gestor"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/faturas/{faturaId}/itens", addItemBody);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var item = await response.Content.ReadFromJsonAsync<ItemAddedDto>(JsonOptions);
        item!.ValorTotalItem.Should().Be(1200m);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> CriarFaturaAsync(string? numero = null)
    {
        var body = new
        {
            Numero = numero ?? GerarNumero(),
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
