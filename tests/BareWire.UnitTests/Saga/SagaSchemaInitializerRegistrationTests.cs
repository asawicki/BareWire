using AwesomeAssertions;
using BareWire.Saga.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BareWire.UnitTests.Saga;

public sealed class SagaSchemaInitializerRegistrationTests
{
    [Fact]
    public void AddBareWireSaga_WhenAutoCreateSchemaTrue_RegistersInitializer()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBareWireSaga<OrderSagaState>(
            configureDbContext: o => o.UseSqlite("DataSource=:memory:"),
            autoCreateSchema: true);

        // Assert
        var hasInitializer = services.Any(d =>
            d.ServiceType == typeof(IHostedService)
            && d.ImplementationType is not null
            && d.ImplementationType.Name == "SagaSchemaInitializer");

        hasInitializer.Should().BeTrue(
            "SagaSchemaInitializer should be registered as IHostedService when autoCreateSchema is true");
    }

    [Fact]
    public void AddBareWireSaga_WhenAutoCreateSchemaFalse_DoesNotRegisterInitializer()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act — autoCreateSchema is omitted; defaults to false
        services.AddBareWireSaga<OrderSagaState>(
            configureDbContext: o => o.UseSqlite("DataSource=:memory:"));

        // Assert
        var hasInitializer = services.Any(d =>
            d.ServiceType == typeof(IHostedService)
            && d.ImplementationType is not null
            && d.ImplementationType.Name == "SagaSchemaInitializer");

        hasInitializer.Should().BeFalse(
            "SagaSchemaInitializer must not be registered when autoCreateSchema is false");
    }
}
