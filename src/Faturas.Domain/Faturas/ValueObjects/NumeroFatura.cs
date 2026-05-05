using System.Text.RegularExpressions;
using Faturas.Domain.Common;
using Faturas.Domain.Faturas.Errors;

namespace Faturas.Domain.Faturas.ValueObjects;

public sealed class NumeroFatura : ValueObject
{
    private static readonly Regex _formato = new(@"^FAT-\d{6}$", RegexOptions.Compiled);

    public string Valor { get; }

    private NumeroFatura(string valor) => Valor = valor;

    public static NumeroFatura Criar(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new DomainException(FaturaErrors.NumeroObrigatorio);

        if (!_formato.IsMatch(valor))
            throw new DomainException(FaturaErrors.NumeroFormatoInvalido);

        return new NumeroFatura(valor);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Valor;
    }

    public override string ToString() => Valor;
}
