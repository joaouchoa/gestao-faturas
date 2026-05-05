using Bogus;

namespace Faturas.Domain.Tests.Faturas.Builders;

public class ItemFaturaFaker : Faker
{
    public string Descricao => Commerce.ProductName();
    public int Quantidade => Random.Int(1, 10);
    public decimal ValorUnitario => Random.Decimal(1m, 100m);
}
