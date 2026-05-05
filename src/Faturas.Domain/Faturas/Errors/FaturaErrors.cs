namespace Faturas.Domain.Faturas.Errors;

public static class FaturaErrors
{
    public const string NomeClienteObrigatorio = "O nome do cliente é obrigatório.";
    public const string NumeroObrigatorio = "O número da fatura é obrigatório.";
    public const string FaturaJaFechada = "Não é possível alterar uma fatura fechada.";
    public const string ItemNaoEncontrado = "Item não encontrado na fatura.";
    public const string JustificativaObrigatoriaAcimaDe1000 = "Itens acima de R$ 1.000,00 exigem justificativa.";
    public const string DescricaoObrigatoria = "A descrição do item é obrigatória.";
    public const string DescricaoTamanhoMinimo = "A descrição deve ter no mínimo 3 caracteres.";
    public const string QuantidadeInvalida = "A quantidade deve ser maior que zero.";
    public const string ValorUnitarioInvalido = "O valor unitário deve ser maior que zero.";
}
