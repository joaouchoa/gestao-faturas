using System.ComponentModel.DataAnnotations;

namespace Faturas.Web.Models.ViewModels;

public class AddItemViewModel
{
    public Guid FaturaId { get; set; }

    [Required(ErrorMessage = "A descrição é obrigatória.")]
    [MinLength(3, ErrorMessage = "A descrição deve ter no mínimo 3 caracteres.")]
    [Display(Name = "Descrição")]
    public string Descricao { get; set; } = "";

    [Range(1, int.MaxValue, ErrorMessage = "A quantidade deve ser maior que zero.")]
    [Display(Name = "Quantidade")]
    public int Quantidade { get; set; } = 1;

    [Range(0.01, double.MaxValue, ErrorMessage = "O valor unitário deve ser maior que zero.")]
    [Display(Name = "Valor unitário")]
    public decimal ValorUnitario { get; set; }

    [Display(Name = "Justificativa")]
    public string? Justificativa { get; set; }
}
