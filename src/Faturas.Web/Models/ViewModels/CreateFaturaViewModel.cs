using System.ComponentModel.DataAnnotations;

namespace Faturas.Web.Models.ViewModels;

public class CreateFaturaViewModel
{
    [Required(ErrorMessage = "O número da fatura é obrigatório.")]
    [Display(Name = "Número")]
    public string Numero { get; set; } = "";

    [Required(ErrorMessage = "O nome do cliente é obrigatório.")]
    [MaxLength(150, ErrorMessage = "O nome do cliente deve ter no máximo 150 caracteres.")]
    [Display(Name = "Nome do cliente")]
    public string NomeCliente { get; set; } = "";

    [Required(ErrorMessage = "A data de emissão é obrigatória.")]
    [Display(Name = "Data de emissão")]
    public DateTime DataEmissao { get; set; } = DateTime.Today;
}
