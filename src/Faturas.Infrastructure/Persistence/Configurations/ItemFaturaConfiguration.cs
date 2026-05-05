using Faturas.Domain.Faturas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faturas.Infrastructure.Persistence.Configurations;

public class ItemFaturaConfiguration : IEntityTypeConfiguration<ItemFatura>
{
    public void Configure(EntityTypeBuilder<ItemFatura> builder)
    {
        builder.ToTable("itens_fatura");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnName("id");

        builder.Property<Guid>("fatura_id")
            .HasColumnName("fatura_id")
            .IsRequired();

        builder.Property(i => i.Descricao)
            .HasColumnName("descricao")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(i => i.Quantidade)
            .HasColumnName("quantidade")
            .IsRequired();

        builder.Property(i => i.ValorUnitario)
            .HasColumnName("valor_unitario")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(i => i.ValorTotalItem)
            .HasColumnName("valor_total_item")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(i => i.Justificativa)
            .HasColumnName("justificativa");
    }
}
