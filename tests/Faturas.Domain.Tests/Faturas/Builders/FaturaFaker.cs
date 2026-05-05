using Bogus;
using Faturas.Domain.Faturas;

namespace Faturas.Domain.Tests.Faturas.Builders;

public class FaturaFaker : Faker<Fatura>
{
    private static int _counter = 1;

    public FaturaFaker()
    {
        CustomInstantiator(f =>
        {
            var numero = $"FAT-{_counter++:D6}";
            var nomeCliente = f.Person.FullName;
            var dataEmissao = f.Date.Recent(30).ToUniversalTime();
            return Fatura.Criar(numero, nomeCliente, dataEmissao);
        });
    }
}
