using Faturas.Domain.Common;
using Faturas.Domain.Faturas.Errors;

namespace Faturas.Domain.Faturas.ValueObjects;

public sealed class NumeroFatura : ValueObject
{
    public string Valor { get; }

    private NumeroFatura(string valor) => Valor = valor;

    public static NumeroFatura Criar(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new DomainException(FaturaErrors.NumeroObrigatorio);

        return new NumeroFatura(valor.Trim());
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Valor;
    }

    public override string ToString() => Valor;
}
