using System.Data.Common;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BareWire.Outbox.EntityFramework;

internal sealed class OutboxSchemaInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxSchemaInitializer> _logger;

    public OutboxSchemaInitializer(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxSchemaInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<OutboxDbContext>();
        var creator = context.Database.GetService<IRelationalDatabaseCreator>();

        try
        {
            await creator.CreateTablesAsync(cancellationToken).ConfigureAwait(false);
            OutboxSchemaInitializerLogMessages.SchemaCreated(_logger);
        }
        catch (DbException ex)
        {
            OutboxSchemaInitializerLogMessages.SchemaCreationSkipped(_logger, ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal static partial class OutboxSchemaInitializerLogMessages
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Outbox/Inbox schema created successfully.")]
    internal static partial void SchemaCreated(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "DbException during Outbox/Inbox schema creation — tables likely already exist, skipping.")]
    internal static partial void SchemaCreationSkipped(ILogger logger, DbException exception);
}
