using Bogus;
using Faturas.Domain.Faturas;

namespace Faturas.Application.Tests.Common.Fakers;

public class FaturaFaker : Faker<Fatura>
{
    private static int _counter = 1;

    public FaturaFaker()
    {
        CustomInstantiator(f =>
        {
            var numero = $"NF-{_counter++:D4}-{f.Random.AlphaNumeric(4).ToUpper()}";
            var nomeCliente = f.Person.FullName;
            var dataEmissao = f.Date.Recent(30).ToUniversalTime();
            return Fatura.Criar(numero, nomeCliente, dataEmissao);
        });
    }
}
