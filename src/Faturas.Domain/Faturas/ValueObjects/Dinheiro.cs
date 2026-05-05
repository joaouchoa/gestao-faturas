using System.Globalization;
using Faturas.Domain.Common;

namespace Faturas.Domain.Faturas.ValueObjects;

public sealed class Dinheiro : ValueObject
{
    public decimal Valor { get; }

    private Dinheiro(decimal valor) => Valor = valor;

    public static Dinheiro Criar(decimal valor)
    {
        if (valor < 0)
            throw new DomainException("O valor monetário não pode ser negativo.");
        return new Dinheiro(valor);
    }

    public static Dinheiro Zero => new(0m);

    public Dinheiro Somar(Dinheiro outro) => new(Valor + outro.Valor);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Valor;
    }

    public override string ToString() => Valor.ToString("C", new CultureInfo("pt-BR"));
}
