namespace Faturas.Web.Models.ViewModels;

public class FaturaDetailsViewModel
{
    public Guid Id { get; set; }
    public string Numero { get; set; } = "";
    public string NomeCliente { get; set; } = "";
    public DateTime DataEmissao { get; set; }
    public string Status { get; set; } = "";
    public decimal ValorTotal { get; set; }
    public List<ItemFaturaViewModel> Itens { get; set; } = [];
    public AddItemViewModel NovoItem { get; set; } = new();
}
