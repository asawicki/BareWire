using Microsoft.EntityFrameworkCore;

namespace BareWire.Samples.InboxDeduplication.Data;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
}
