namespace Faturas.Web.Models.ViewModels;

public class FaturaFilterViewModel
{
    public string? Cliente { get; set; }
    public DateTime? DataInicial { get; set; }
    public DateTime? DataFinal { get; set; }
    public string? Status { get; set; }
    public int Pagina { get; set; } = 1;
    public int TamanhoPagina { get; set; } = 10;
}
