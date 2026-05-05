namespace Faturas.Web.Models.ViewModels;

public class FaturaListViewModel
{
    public List<FaturaListItemViewModel> Faturas { get; set; } = [];
    public FaturaFilterViewModel Filtro { get; set; } = new();
}
