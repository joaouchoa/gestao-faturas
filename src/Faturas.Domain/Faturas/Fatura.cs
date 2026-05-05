using Faturas.Domain.Common;
using Faturas.Domain.Faturas.Errors;
using Faturas.Domain.Faturas.Events;
using Faturas.Domain.Faturas.ValueObjects;

namespace Faturas.Domain.Faturas;

public class Fatura : AggregateRoot
{
    private readonly List<ItemFatura> _itens = [];

    public NumeroFatura Numero { get; private set; } = null!;
    public string NomeCliente { get; private set; } = string.Empty;
    public DateTime DataEmissao { get; private set; }
    public StatusFatura Status { get; private set; }
    public decimal ValorTotal { get; private set; }
    public IReadOnlyCollection<ItemFatura> Itens => _itens.AsReadOnly();

    private Fatura() { }

    private Fatura(NumeroFatura numero, string nomeCliente, DateTime dataEmissao) : base()
    {
        Numero = numero;
        NomeCliente = nomeCliente;
        DataEmissao = dataEmissao;
        Status = StatusFatura.Aberta; // RN-1
        ValorTotal = 0m;
    }

    public static Fatura Criar(string numero, string nomeCliente, DateTime dataEmissao)
    {
        // RN-2
        if (string.IsNullOrWhiteSpace(nomeCliente))
            throw new DomainException(FaturaErrors.NomeClienteObrigatorio);

        var numeroFatura = NumeroFatura.Criar(numero);
        var fatura = new Fatura(numeroFatura, nomeCliente.Trim(), dataEmissao.ToUniversalTime());
        fatura.RaiseDomainEvent(new FaturaCriadaEvent(fatura.Id, numero));
        return fatura;
    }

    public ItemFatura AdicionarItem(string descricao, int quantidade, decimal valorUnitario, string? justificativa = null)
    {
        GuardarFechada(); // RN-5/6

        var item = ItemFatura.Criar(descricao, quantidade, valorUnitario, justificativa);
        _itens.Add(item);
        RecalcularTotal(); // RN-4

        RaiseDomainEvent(new ItemAdicionadoEvent(Id, item.Id));
        return item;
    }

    public void RemoverItem(Guid itemId)
    {
        GuardarFechada(); // RN-5/6

        var item = _itens.FirstOrDefault(i => i.Id == itemId)
            ?? throw new DomainException(FaturaErrors.ItemNaoEncontrado);

        _itens.Remove(item);
        RecalcularTotal(); // RN-4
    }

    public void AtualizarItem(Guid itemId, string descricao, int quantidade, decimal valorUnitario, string? justificativa = null)
    {
        GuardarFechada(); // RN-5/6

        var item = _itens.FirstOrDefault(i => i.Id == itemId)
            ?? throw new DomainException(FaturaErrors.ItemNaoEncontrado);

        item.Atualizar(descricao, quantidade, valorUnitario, justificativa);
        RecalcularTotal(); // RN-4
    }

    public void Fechar()
    {
        GuardarFechada(); // RN-9
        Status = StatusFatura.Fechada;
        RaiseDomainEvent(new FaturaFechadaEvent(Id));
    }

    private void GuardarFechada()
    {
        if (Status == StatusFatura.Fechada)
            throw new DomainException(FaturaErrors.FaturaJaFechada);
    }

    private void RecalcularTotal() =>
        ValorTotal = _itens.Sum(i => i.ValorTotalItem);
}
