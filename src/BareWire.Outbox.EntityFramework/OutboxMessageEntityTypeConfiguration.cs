using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BareWire.Outbox.EntityFramework;

internal sealed class OutboxMessageEntityTypeConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .ValueGeneratedOnAdd();

        builder.Property(m => m.MessageId)
            .IsRequired();

        builder.Property(m => m.DestinationAddress)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(m => m.ContentType)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(m => m.Payload)
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .IsRequired();

        builder.HasIndex(m => m.MessageId);

        builder.HasIndex(m => m.CreatedAt);

        builder.HasIndex(m => m.DeliveredAt);
    }
}
