using System.Net.Http.Json;
using System.Text.Json;
using Faturas.Web.Models.ViewModels;
using Microsoft.AspNetCore.WebUtilities;

namespace Faturas.Web.Services;

public class FaturasApiClient : IFaturasApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<FaturasApiClient> _logger;

    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FaturasApiClient(HttpClient http, ILogger<FaturasApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<(List<FaturaListItemViewModel> Itens, int TotalRegistros, int TotalPaginas)> ListAsync(
        string? cliente, DateTime? dataInicial, DateTime? dataFinal, string? status,
        int pagina = 1, int tamanhoPagina = 10,
        CancellationToken ct = default)
    {
        var query = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(cliente)) query["cliente"]       = cliente;
        if (dataInicial.HasValue)                query["dataInicial"]   = dataInicial.Value.ToString("yyyy-MM-dd");
        if (dataFinal.HasValue)                  query["dataFinal"]     = dataFinal.Value.ToString("yyyy-MM-dd");
        if (!string.IsNullOrWhiteSpace(status))  query["status"]        = status;
        query["pagina"]       = pagina.ToString();
        query["tamanhoPagina"] = tamanhoPagina.ToString();

        var url = QueryHelpers.AddQueryString("/api/faturas", query);

        try
        {
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return ([], 0, 0);

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc  = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var itens = root.GetProperty("itens")
                .Deserialize<List<FaturaListItemViewModel>>(_opts) ?? [];
            var total       = root.GetProperty("totalRegistros").GetInt32();
            var totalPaginas = root.GetProperty("totalPaginas").GetInt32();

            return (itens, total, totalPaginas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar faturas");
            return ([], 0, 0);
        }
    }

    public async Task<FaturaDetailsViewModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"/api/faturas/{id}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var vm = new FaturaDetailsViewModel
            {
                Id          = root.GetProperty("id").GetGuid(),
                Numero      = root.GetProperty("numero").GetString() ?? "",
                NomeCliente = root.GetProperty("nomeCliente").GetString() ?? "",
                DataEmissao = root.GetProperty("dataEmissao").GetDateTime(),
                Status      = root.GetProperty("status").GetString() ?? "",
                ValorTotal  = root.GetProperty("valorTotal").GetDecimal()
            };

            foreach (var item in root.GetProperty("itens").EnumerateArray())
            {
                vm.Itens.Add(new ItemFaturaViewModel
                {
                    Id            = item.GetProperty("id").GetGuid(),
                    Descricao     = item.GetProperty("descricao").GetString() ?? "",
                    Quantidade    = item.GetProperty("quantidade").GetInt32(),
                    ValorUnitario = item.GetProperty("valorUnitario").GetDecimal(),
                    ValorTotalItem = item.GetProperty("valorTotalItem").GetDecimal(),
                    Justificativa = item.TryGetProperty("justificativa", out var j) ? j.GetString() : null
                });
            }

            return vm;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar fatura {Id}", id);
            return null;
        }
    }

    public async Task<(Guid? Id, Dictionary<string, string[]>? Errors)> CreateAsync(CreateFaturaViewModel vm, CancellationToken ct = default)
    {
        try
        {
            var body = new
            {
                numero      = vm.Numero,
                nomeCliente = vm.NomeCliente,
                dataEmissao = vm.DataEmissao
            };

            var response = await _http.PostAsJsonAsync("/api/faturas", body, ct);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<FaturaListItemViewModel>(_opts, ct);
                return (result?.Id, null);
            }

            return (null, await ParseFieldErrors(response, ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar fatura");
            return (null, new Dictionary<string, string[]> { [string.Empty] = ["Erro ao comunicar com a API."] });
        }
    }

    private static async Task<Dictionary<string, string[]>> ParseFieldErrors(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            var doc     = JsonDocument.Parse(content);
            var root    = doc.RootElement;

            // ValidationProblemDetails: dicionário errors com erros por campo
            if (root.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Object)
            {
                var dict = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in errors.EnumerateObject())
                {
                    var msgs = prop.Value.EnumerateArray()
                        .Select(v => v.GetString() ?? "")
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToArray();
                    if (msgs.Length > 0)
                        dict[prop.Name] = msgs;
                }
                if (dict.Count > 0) return dict;
            }

            // DomainException (422): campo detail
            if (root.TryGetProperty("detail", out var detail) &&
                detail.ValueKind == JsonValueKind.String)
                return new Dictionary<string, string[]> { [string.Empty] = [detail.GetString()!] };

            if (root.TryGetProperty("title", out var title) &&
                title.ValueKind == JsonValueKind.String)
                return new Dictionary<string, string[]> { [string.Empty] = [title.GetString()!] };
        }
        catch { }

        return new Dictionary<string, string[]>
        {
            [string.Empty] = [$"Erro {(int)response.StatusCode}: {response.ReasonPhrase}"]
        };
    }

    public async Task<string?> AddItemAsync(Guid faturaId, AddItemViewModel vm, CancellationToken ct = default)
    {
        try
        {
            var body = new
            {
                descricao     = vm.Descricao,
                quantidade    = vm.Quantidade,
                valorUnitario = vm.ValorUnitario,
                justificativa = vm.Justificativa
            };

            var response = await _http.PostAsJsonAsync($"/api/faturas/{faturaId}/itens", body, ct);
            if (response.IsSuccessStatusCode) return null;
            return await ParseError(response, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao adicionar item à fatura {FaturaId}", faturaId);
            return "Erro ao comunicar com a API.";
        }
    }

    public async Task<string?> FecharAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PutAsync($"/api/faturas/{id}/fechar", null, ct);
            if (response.IsSuccessStatusCode) return null;
            return await ParseError(response, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao fechar fatura {Id}", id);
            return "Erro ao comunicar com a API.";
        }
    }

    public async Task<string?> RemoveItemAsync(Guid faturaId, Guid itemId, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.DeleteAsync($"/api/faturas/{faturaId}/itens/{itemId}", ct);
            if (response.IsSuccessStatusCode) return null;
            return await ParseError(response, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover item {ItemId} da fatura {FaturaId}", itemId, faturaId);
            return "Erro ao comunicar com a API.";
        }
    }

    private static async Task<string> ParseError(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            // ValidationProblemDetails (400): lê o dicionário errors com as mensagens por campo
            if (root.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Object)
            {
                var messages = new List<string>();
                foreach (var prop in errors.EnumerateObject())
                {
                    foreach (var msg in prop.Value.EnumerateArray())
                    {
                        var text = msg.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                            messages.Add(text);
                    }
                }
                if (messages.Count > 0)
                    return string.Join(" | ", messages);
            }

            // DomainException (422): lê o campo detail
            if (root.TryGetProperty("detail", out var detail) &&
                detail.ValueKind == JsonValueKind.String)
                return detail.GetString() ?? "Erro desconhecido.";

            if (root.TryGetProperty("title", out var title) &&
                title.ValueKind == JsonValueKind.String)
                return title.GetString() ?? "Erro desconhecido.";
        }
        catch { }

        return $"Erro {(int)response.StatusCode}: {response.ReasonPhrase}";
    }
}
