using AwesomeAssertions;
using BareWire.Abstractions;
using BareWire.Abstractions.Serialization;
using BareWire.Abstractions.Transport;
using BareWire.Saga.Scheduling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace BareWire.UnitTests.Saga;

public sealed class ScheduleProviderFactoryTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ITransportAdapter CreateTransportAdapter()
    {
        var adapter = Substitute.For<ITransportAdapter>();
        adapter.TransportName.Returns("RabbitMQ");
        return adapter;
    }

    private static NullLoggerFactory CreateLoggerFactory()
        => NullLoggerFactory.Instance;

    private static IMessageSerializer CreateSerializer()
    {
        var serializer = Substitute.For<IMessageSerializer>();
        serializer.ContentType.Returns("application/json");
        return serializer;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_Auto_ReturnsDelayRequeueProvider()
    {
        var transport = CreateTransportAdapter();
        var loggerFactory = CreateLoggerFactory();
        var serializer = CreateSerializer();

        var provider = ScheduleProviderFactory.Create(SchedulingStrategy.Auto, transport, loggerFactory, serializer);

        provider.Should().BeOfType<DelayRequeueScheduleProvider>();
    }

    [Fact]
    public void Create_DelayRequeue_ReturnsDelayRequeueProvider()
    {
        var transport = CreateTransportAdapter();
        var loggerFactory = CreateLoggerFactory();
        var serializer = CreateSerializer();

        var provider = ScheduleProviderFactory.Create(SchedulingStrategy.DelayRequeue, transport, loggerFactory, serializer);

        provider.Should().BeOfType<DelayRequeueScheduleProvider>();
    }

    [Fact]
    public void Create_TransportNative_ThrowsNotSupportedException()
    {
        var transport = CreateTransportAdapter();
        var loggerFactory = CreateLoggerFactory();
        var serializer = CreateSerializer();

        Action act = () => ScheduleProviderFactory.Create(SchedulingStrategy.TransportNative, transport, loggerFactory, serializer);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Create_ExternalScheduler_ThrowsNotSupportedException()
    {
        var transport = CreateTransportAdapter();
        var loggerFactory = CreateLoggerFactory();
        var serializer = CreateSerializer();

        Action act = () => ScheduleProviderFactory.Create(SchedulingStrategy.ExternalScheduler, transport, loggerFactory, serializer);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Create_DelayTopic_ThrowsNotSupportedException()
    {
        var transport = CreateTransportAdapter();
        var loggerFactory = CreateLoggerFactory();
        var serializer = CreateSerializer();

        Action act = () => ScheduleProviderFactory.Create(SchedulingStrategy.DelayTopic, transport, loggerFactory, serializer);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Create_NullTransport_ThrowsArgumentNullException()
    {
        var loggerFactory = CreateLoggerFactory();
        var serializer = CreateSerializer();

        Action act = () => ScheduleProviderFactory.Create(SchedulingStrategy.Auto, null!, loggerFactory, serializer);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_NullLoggerFactory_ThrowsArgumentNullException()
    {
        var transport = CreateTransportAdapter();
        var serializer = CreateSerializer();

        Action act = () => ScheduleProviderFactory.Create(SchedulingStrategy.Auto, transport, null!, serializer);

        act.Should().Throw<ArgumentNullException>();
    }
}
