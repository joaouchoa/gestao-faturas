using Faturas.Domain.Faturas;
using Faturas.Domain.Faturas.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faturas.Infrastructure.Persistence.Configurations;

public class FaturaConfiguration : IEntityTypeConfiguration<Fatura>
{
    public void Configure(EntityTypeBuilder<Fatura> builder)
    {
        builder.ToTable("faturas");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .HasColumnName("id");

        builder.Property(f => f.Numero)
            .HasColumnName("numero")
            .HasMaxLength(20)
            .IsRequired()
            .HasConversion(
                numero => numero.Valor,
                valor  => NumeroFatura.Criar(valor));

        builder.Property(f => f.NomeCliente)
            .HasColumnName("nome_cliente")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(f => f.DataEmissao)
            .HasColumnName("data_emissao")
            .IsRequired();

        builder.Property(f => f.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(f => f.ValorTotal)
            .HasColumnName("valor_total")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasMany(f => f.Itens)
            .WithOne()
            .HasForeignKey("fatura_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(f => f.Itens)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
