using AwesomeAssertions;
using BareWire.Outbox.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BareWire.UnitTests.Outbox;

public sealed class OutboxSchemaInitializerRegistrationTests
{
    [Fact]
    public void AddBareWireOutbox_WhenAutoCreateSchemaTrue_RegistersInitializer()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBareWireOutbox(
            configureDbContext: o => o.UseSqlite("DataSource=:memory:"),
            configureOutbox: outbox => { outbox.AutoCreateSchema = true; });

        // Assert
        var hasInitializer = services.Any(d =>
            d.ServiceType == typeof(IHostedService)
            && d.ImplementationType is not null
            && d.ImplementationType.Name == "OutboxSchemaInitializer");

        hasInitializer.Should().BeTrue(
            "OutboxSchemaInitializer should be registered as IHostedService when AutoCreateSchema is true");
    }

    [Fact]
    public void AddBareWireOutbox_WhenAutoCreateSchemaFalse_DoesNotRegisterInitializer()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act — configureOutbox is omitted; AutoCreateSchema defaults to false
        services.AddBareWireOutbox(
            configureDbContext: o => o.UseSqlite("DataSource=:memory:"));

        // Assert
        var hasInitializer = services.Any(d =>
            d.ServiceType == typeof(IHostedService)
            && d.ImplementationType is not null
            && d.ImplementationType.Name == "OutboxSchemaInitializer");

        hasInitializer.Should().BeFalse(
            "OutboxSchemaInitializer must not be registered when AutoCreateSchema is false");
    }
}
