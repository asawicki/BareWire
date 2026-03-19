using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BareWire.Outbox.EntityFramework;

internal sealed class InboxMessageEntityTypeConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.HasKey(m => new { m.MessageId, m.ConsumerType });

        builder.Property(m => m.MessageId)
            .IsRequired();

        builder.Property(m => m.ConsumerType)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(m => m.ReceivedAt)
            .IsRequired();

        builder.Property(m => m.ExpiresAt)
            .IsRequired();

        builder.HasIndex(m => m.ExpiresAt);
    }
}
