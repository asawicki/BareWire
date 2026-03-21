using System.Data.Common;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BareWire.Saga.EntityFramework;

internal sealed class SagaSchemaInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SagaSchemaInitializer> _logger;

    public SagaSchemaInitializer(
        IServiceScopeFactory scopeFactory,
        ILogger<SagaSchemaInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SagaDbContext>();
        var creator = context.Database.GetService<IRelationalDatabaseCreator>();

        try
        {
            await creator.CreateTablesAsync(cancellationToken).ConfigureAwait(false);
            SagaSchemaInitializerLogMessages.SchemaCreated(_logger);
        }
        catch (DbException ex)
        {
            SagaSchemaInitializerLogMessages.SchemaCreationSkipped(_logger, ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal static partial class SagaSchemaInitializerLogMessages
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Saga schema created successfully.")]
    internal static partial void SchemaCreated(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "DbException during Saga schema creation — tables likely already exist, skipping.")]
    internal static partial void SchemaCreationSkipped(ILogger logger, DbException exception);
}
