using System.Buffers;
using System.Text;
using AwesomeAssertions;
using BareWire.Abstractions;
using BareWire.Abstractions.Pipeline;
using BareWire.Testing;

namespace BareWire.UnitTests.Testing;

public sealed class MessageContextBuilderTests
{
    private sealed record TestMessage(string Value);

    [Fact]
    public void Build_WithPayload_ReturnsTypedContext()
    {
        TestMessage message = new("hello");

        ConsumeContext<TestMessage> context = MessageContextBuilder
            .Create()
            .Build(message);

        context.Should().NotBeNull();
        context.Message.Should().BeSameAs(message);
        context.Message.Value.Should().Be("hello");
    }

    [Fact]
    public void Build_WithMessageId_SetsMessageId()
    {
        Guid expectedId = Guid.NewGuid();

        ConsumeContext<TestMessage> context = MessageContextBuilder
            .Create()
            .WithMessageId(expectedId)
            .Build(new TestMessage("x"));

        context.MessageId.Should().Be(expectedId);
    }

    [Fact]
    public void Build_WithCorrelationId_SetsCorrelationId()
    {
        Guid correlationId = Guid.NewGuid();

        ConsumeContext<TestMessage> context = MessageContextBuilder
            .Create()
            .WithCorrelationId(correlationId)
            .Build(new TestMessage("x"));

        context.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public void Build_WithConversationId_SetsConversationId()
    {
        Guid conversationId = Guid.NewGuid();

        ConsumeContext<TestMessage> context = MessageContextBuilder
            .Create()
            .WithConversationId(conversationId)
            .Build(new TestMessage("x"));

        context.ConversationId.Should().Be(conversationId);
    }

    [Fact]
    public void Build_WithHeaders_SetsHeaders()
    {
        var headers = new Dictionary<string, string>
        {
            ["x-correlation-id"] = "abc",
            ["x-trace-id"] = "xyz",
        };

        ConsumeContext<TestMessage> context = MessageContextBuilder
            .Create()
            .WithHeaders(headers)
            .Build(new TestMessage("x"));

        context.Headers.Should().ContainKey("x-correlation-id").WhoseValue.Should().Be("abc");
        context.Headers.Should().ContainKey("x-trace-id").WhoseValue.Should().Be("xyz");
    }

    [Fact]
    public void Build_DefaultValues_GeneratesMessageId()
    {
        ConsumeContext<TestMessage> context1 = MessageContextBuilder.Create().Build(new TestMessage("a"));
        ConsumeContext<TestMessage> context2 = MessageContextBuilder.Create().Build(new TestMessage("b"));

        context1.MessageId.Should().NotBe(Guid.Empty);
        context2.MessageId.Should().NotBe(Guid.Empty);
        context1.MessageId.Should().NotBe(context2.MessageId);
    }

    [Fact]
    public void Build_DefaultValues_HeadersAreEmpty()
    {
        ConsumeContext<TestMessage> context = MessageContextBuilder.Create().Build(new TestMessage("x"));

        context.Headers.Should().BeEmpty();
    }

    [Fact]
    public void Build_DefaultValues_OptionalFieldsAreNull()
    {
        ConsumeContext<TestMessage> context = MessageContextBuilder.Create().Build(new TestMessage("x"));

        context.CorrelationId.Should().BeNull();
        context.ConversationId.Should().BeNull();
        context.SourceAddress.Should().BeNull();
        context.DestinationAddress.Should().BeNull();
        context.SentTime.Should().BeNull();
        context.ContentType.Should().BeNull();
    }

    [Fact]
    public void Build_WithSourceAddress_SetsSourceAddress()
    {
        Uri address = new("barewire://test/source");

        ConsumeContext<TestMessage> context = MessageContextBuilder
            .Create()
            .WithSourceAddress(address)
            .Build(new TestMessage("x"));

        context.SourceAddress.Should().Be(address);
    }

    [Fact]
    public void Build_WithDestinationAddress_SetsDestinationAddress()
    {
        Uri address = new("barewire://test/dest");

        ConsumeContext<TestMessage> context = MessageContextBuilder
            .Create()
            .WithDestinationAddress(address)
            .Build(new TestMessage("x"));

        context.DestinationAddress.Should().Be(address);
    }

    [Fact]
    public void Build_WithSentTime_SetsSentTime()
    {
        DateTimeOffset sentTime = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

        ConsumeContext<TestMessage> context = MessageContextBuilder
            .Create()
            .WithSentTime(sentTime)
            .Build(new TestMessage("x"));

        context.SentTime.Should().Be(sentTime);
    }

    [Fact]
    public void Build_WithContentType_SetsContentType()
    {
        ConsumeContext<TestMessage> context = MessageContextBuilder
            .Create()
            .WithContentType("application/json")
            .Build(new TestMessage("x"));

        context.ContentType.Should().Be("application/json");
    }

    [Fact]
    public void Build_WithCancellationToken_SetsCancellationToken()
    {
        using CancellationTokenSource cts = new();

        ConsumeContext<TestMessage> context = MessageContextBuilder
            .Create()
            .WithCancellationToken(cts.Token)
            .Build(new TestMessage("x"));

        context.CancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public void Build_WithRawBody_SetsRawBody()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("raw-payload");
        ReadOnlySequence<byte> body = new(bytes);

        ConsumeContext<TestMessage> context = MessageContextBuilder
            .Create()
            .WithRawBody(body)
            .Build(new TestMessage("x"));

        context.RawBody.ToArray().Should().Equal(bytes);
    }

    [Fact]
    public void BuildMessageContext_ReturnsMessageContext()
    {
        Guid messageId = Guid.NewGuid();
        var headers = new Dictionary<string, string> { ["key"] = "value" };

        MessageContext context = MessageContextBuilder
            .Create()
            .WithMessageId(messageId)
            .WithHeaders(headers)
            .BuildMessageContext();

        context.Should().NotBeNull();
        context.MessageId.Should().Be(messageId);
        context.Headers.Should().ContainKey("key").WhoseValue.Should().Be("value");
        context.ServiceProvider.Should().NotBeNull();
    }

    [Fact]
    public void BuildMessageContext_DefaultValues_GeneratesMessageId()
    {
        MessageContext context = MessageContextBuilder.Create().BuildMessageContext();

        context.MessageId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void BuildMessageContext_WithCancellationToken_SetsCancellationToken()
    {
        using CancellationTokenSource cts = new();

        MessageContext context = MessageContextBuilder
            .Create()
            .WithCancellationToken(cts.Token)
            .BuildMessageContext();

        context.CancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public void MessageContextBuilder_WithEndpointName_SetsProperty()
    {
        // Act
        MessageContext context = MessageContextBuilder
            .Create()
            .WithEndpointName("my-endpoint")
            .BuildMessageContext();

        // Assert
        context.EndpointName.Should().Be("my-endpoint");
    }

    [Fact]
    public void MessageContextBuilder_Default_SetsEmptyEndpointName()
    {
        // Act
        MessageContext context = MessageContextBuilder
            .Create()
            .BuildMessageContext();

        // Assert
        context.EndpointName.Should().BeEmpty();
    }
}
