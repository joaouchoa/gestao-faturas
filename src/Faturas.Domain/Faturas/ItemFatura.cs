using Faturas.Domain.Common;
using Faturas.Domain.Faturas.Errors;

namespace Faturas.Domain.Faturas;

public class ItemFatura : Entity
{
    public string Descricao { get; private set; } = string.Empty;
    public int Quantidade { get; private set; }
    public decimal ValorUnitario { get; private set; }
    public decimal ValorTotalItem { get; private set; }
    public string? Justificativa { get; private set; }

    private ItemFatura() { }

    private ItemFatura(string descricao, int quantidade, decimal valorUnitario, string? justificativa) : base()
    {
        Descricao = descricao;
        Quantidade = quantidade;
        ValorUnitario = valorUnitario;
        ValorTotalItem = quantidade * valorUnitario;
        Justificativa = justificativa;
    }

    internal static ItemFatura Criar(string descricao, int quantidade, decimal valorUnitario, string? justificativa)
    {
        Validar(descricao, quantidade, valorUnitario, justificativa);
        return new ItemFatura(descricao.Trim(), quantidade, valorUnitario, justificativa?.Trim());
    }

    internal void Atualizar(string descricao, int quantidade, decimal valorUnitario, string? justificativa)
    {
        Validar(descricao, quantidade, valorUnitario, justificativa);
        Descricao = descricao.Trim();
        Quantidade = quantidade;
        ValorUnitario = valorUnitario;
        ValorTotalItem = quantidade * valorUnitario;
        Justificativa = justificativa?.Trim();
    }

    private static void Validar(string descricao, int quantidade, decimal valorUnitario, string? justificativa)
    {
        if (string.IsNullOrWhiteSpace(descricao))
            throw new DomainException(FaturaErrors.DescricaoObrigatoria);

        if (descricao.Trim().Length < 3)
            throw new DomainException(FaturaErrors.DescricaoTamanhoMinimo);

        if (quantidade <= 0)
            throw new DomainException(FaturaErrors.QuantidadeInvalida);

        if (valorUnitario <= 0)
            throw new DomainException(FaturaErrors.ValorUnitarioInvalido);

        // RN-7: item > R$ 1.000 exige justificativa
        if (quantidade * valorUnitario > 1000m && string.IsNullOrWhiteSpace(justificativa))
            throw new DomainException(FaturaErrors.JustificativaObrigatoriaAcimaDe1000);
    }
}
