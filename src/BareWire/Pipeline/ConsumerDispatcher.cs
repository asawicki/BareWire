using BareWire.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BareWire.Pipeline;

internal sealed class ConsumerDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConsumerDispatcher> _logger;

    internal ConsumerDispatcher(IServiceScopeFactory scopeFactory, ILogger<ConsumerDispatcher> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    internal async Task DispatchAsync<T>(ConsumeContext<T> context) where T : class
    {
        ArgumentNullException.ThrowIfNull(context);

        IServiceScope scope = _scopeFactory.CreateScope();
        try
        {
            IConsumer<T> consumer = scope.ServiceProvider.GetRequiredService<IConsumer<T>>();
            await consumer.ConsumeAsync(context).ConfigureAwait(false);
        }
        finally
        {
            scope.Dispose();
        }
    }

    internal async Task DispatchRawAsync(RawConsumeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IServiceScope scope = _scopeFactory.CreateScope();
        try
        {
            IRawConsumer consumer = scope.ServiceProvider.GetRequiredService<IRawConsumer>();
            await consumer.ConsumeAsync(context).ConfigureAwait(false);
        }
        finally
        {
            scope.Dispose();
        }
    }
}
