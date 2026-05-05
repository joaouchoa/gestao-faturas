using Faturas.Web.Models.ViewModels;
using Faturas.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Faturas.Web.Controllers;

public class FaturasController : Controller
{
    private readonly IFaturasApiClient _client;

    public FaturasController(IFaturasApiClient client) => _client = client;

    public async Task<IActionResult> Index(string? cliente, DateTime? dataInicial, DateTime? dataFinal, string? status)
    {
        var faturas = await _client.ListAsync(cliente, dataInicial, dataFinal, status);
        var vm = new FaturaListViewModel
        {
            Faturas = faturas,
            Filtro  = new FaturaFilterViewModel
            {
                Cliente      = cliente,
                DataInicial  = dataInicial,
                DataFinal    = dataFinal,
                Status       = status
            }
        };
        return View(vm);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var vm = await _client.GetByIdAsync(id);
        if (vm is null) return NotFound();
        vm.NovoItem.FaturaId = id;
        return View(vm);
    }

    public IActionResult Create() => View(new CreateFaturaViewModel { DataEmissao = DateTime.Today });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateFaturaViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var (id, errors) = await _client.CreateAsync(vm);
        if (errors is not null)
        {
            foreach (var (field, messages) in errors)
                foreach (var msg in messages)
                    ModelState.AddModelError(field, msg);
            return View(vm);
        }

        TempData["Sucesso"] = "Fatura criada com sucesso.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddItem(AddItemViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            var erros = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage);
            TempData["Erro"] = string.Join(" | ", erros);
            return RedirectToAction(nameof(Details), new { id = vm.FaturaId });
        }

        var error = await _client.AddItemAsync(vm.FaturaId, vm);
        if (error is not null)
            TempData["Erro"] = error;
        else
            TempData["Sucesso"] = "Item adicionado com sucesso.";

        return RedirectToAction(nameof(Details), new { id = vm.FaturaId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Fechar(Guid id)
    {
        var error = await _client.FecharAsync(id);
        if (error is not null)
            TempData["Erro"] = error;
        else
            TempData["Sucesso"] = "Fatura fechada com sucesso.";

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveItem(Guid faturaId, Guid itemId)
    {
        var error = await _client.RemoveItemAsync(faturaId, itemId);
        if (error is not null)
            TempData["Erro"] = error;
        else
            TempData["Sucesso"] = "Item removido com sucesso.";

        return RedirectToAction(nameof(Details), new { id = faturaId });
    }
}
