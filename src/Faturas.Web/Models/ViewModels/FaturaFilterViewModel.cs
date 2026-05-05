namespace Faturas.Web.Models.ViewModels;

public class FaturaFilterViewModel
{
    public string? Cliente { get; set; }
    public DateTime? DataInicial { get; set; }
    public DateTime? DataFinal { get; set; }
    public string? Status { get; set; }
}
