namespace Faturas.Web.Models.ViewModels;

public class ItemFaturaViewModel
{
    public Guid Id { get; set; }
    public string Descricao { get; set; } = "";
    public int Quantidade { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal ValorTotalItem { get; set; }
    public string? Justificativa { get; set; }
}
