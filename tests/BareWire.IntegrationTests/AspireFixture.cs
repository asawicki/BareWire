using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BareWire.IntegrationTests;

public sealed class AspireFixture : IAsyncLifetime
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(60);
    private DistributedApplication? _app;
    private string? _rabbitMqConnectionString;

    public async ValueTask InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.BareWire_AppHost>();
        _app = await builder.BuildAsync();
        var notifier = _app.Services.GetRequiredService<ResourceNotificationService>();
        await _app.StartAsync();

        using var cts = new CancellationTokenSource(StartupTimeout);
        await notifier.WaitForResourceHealthyAsync("rmq", cts.Token);

        _rabbitMqConnectionString = await _app.GetConnectionStringAsync("rmq");
    }

    public string GetRabbitMqConnectionString()
    {
        return _rabbitMqConnectionString
            ?? throw new InvalidOperationException("RabbitMQ connection string not available");
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
