namespace Faturas.Application.Common.Errors;

public static class ApplicationErrorMessages
{
    public static class Fatura
    {
        public const string NumeroObrigatorio          = "O número da fatura é obrigatório.";
        public const string NumeroFormatoInvalido      = "O número da fatura deve ter o formato FAT-XXXXXX (ex: FAT-000001).";
        public const string NomeClienteObrigatorio     = "O nome do cliente é obrigatório.";
        public const string NomeClienteTamanhoMaximo   = "O nome do cliente deve ter no máximo 150 caracteres.";
        public const string DataEmissaoObrigatoria     = "A data de emissão é obrigatória.";
        public const string FaturaNaoEncontrada        = "Fatura não encontrada.";
    }

    public static class ItemFatura
    {
        public const string FaturaIdObrigatorio        = "O identificador da fatura é obrigatório.";
        public const string ItemIdObrigatorio          = "O identificador do item é obrigatório.";
        public const string DescricaoObrigatoria       = "A descrição do item é obrigatória.";
        public const string DescricaoTamanhoMinimo     = "A descrição deve ter no mínimo 3 caracteres.";
        public const string QuantidadeInvalida         = "A quantidade deve ser maior que zero.";
        public const string ValorUnitarioInvalido      = "O valor unitário deve ser maior que zero.";
        public const string ItemNaoEncontrado          = "Item não encontrado na fatura.";
    }

    public static class ListFaturas
    {
        public const string PeriodoInvalido            = "A data inicial não pode ser maior que a data final.";
    }
}
