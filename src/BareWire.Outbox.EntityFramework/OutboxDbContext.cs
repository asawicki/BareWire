using Microsoft.EntityFrameworkCore;

namespace BareWire.Outbox.EntityFramework;

/// <summary>
/// DbContext for transactional outbox and inbox persistence.
/// Exposes <see cref="OutboxMessages"/> and <see cref="InboxMessages"/> DbSets
/// and applies the corresponding EF Core entity type configurations.
/// </summary>
/// <remarks>
/// This class is intentionally <c>public</c> and non-<c>sealed</c> to satisfy EF Core's
/// requirements for code-first migrations (<c>dotnet ef migrations add</c>) and the
/// <c>OnModelCreating</c> override pattern. All other implementation classes in this
/// assembly follow the <c>internal sealed</c> convention.
/// </remarks>
public class OutboxDbContext : DbContext
{
    /// <summary>
    /// Gets the DbSet of outbox messages pending delivery.
    /// </summary>
    /// <remarks>
    /// <c>internal</c> because <see cref="OutboxMessage"/> is an internal entity type.
    /// The DbContext itself is <c>public</c> for EF Core migration tooling requirements.
    /// </remarks>
    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>
    /// Gets the DbSet of processed inbox messages used for idempotency.
    /// </summary>
    /// <remarks>
    /// <c>internal</c> because <see cref="InboxMessage"/> is an internal entity type.
    /// The DbContext itself is <c>public</c> for EF Core migration tooling requirements.
    /// </remarks>
    internal DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    /// <summary>
    /// Initializes a new instance of <see cref="OutboxDbContext"/>.
    /// </summary>
    /// <param name="options">The EF Core options configured for this context.</param>
    /// <remarks>
    /// The constructor is <c>public</c> because <c>AddDbContext</c> requires a public
    /// constructor, and EF Core migration tooling also needs to instantiate the context.
    /// </remarks>
    public OutboxDbContext(DbContextOptions<OutboxDbContext> options)
        : base(options)
    {
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new OutboxMessageEntityTypeConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageEntityTypeConfiguration());
    }
}
