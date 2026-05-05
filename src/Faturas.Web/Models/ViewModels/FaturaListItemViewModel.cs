namespace Faturas.Web.Models.ViewModels;

public class FaturaListItemViewModel
{
    public Guid Id { get; set; }
    public string Numero { get; set; } = "";
    public string NomeCliente { get; set; } = "";
    public DateTime DataEmissao { get; set; }
    public string Status { get; set; } = "";
    public decimal ValorTotal { get; set; }
    public int TotalItens { get; set; }
}
