using BareWire.Abstractions;
using BareWire.Bus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace BareWire.IntegrationTests.Bus;

public sealed class BareWireBusHostedServiceTests
{
    [Fact]
    public async Task HostedService_StartsAndStopsBus_WithHostBuilder()
    {
        // Arrange
        IBusControl busControl = Substitute.For<IBusControl>();
        busControl
            .StartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new BusHandle(Guid.NewGuid())));
        busControl
            .StopAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        using IHost host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(busControl);
                services.AddSingleton<IHostedService, BareWireBusHostedService>();
            })
            .Build();

        // Act — start triggers IHostedService.StartAsync
        await host.StartAsync();

        // Assert — bus was started
        await busControl.Received(1).StartAsync(Arg.Any<CancellationToken>());

        // Act — stop triggers IHostedService.StopAsync
        await host.StopAsync();

        // Assert — bus was stopped
        await busControl.Received(1).StopAsync(Arg.Any<CancellationToken>());
    }
}
