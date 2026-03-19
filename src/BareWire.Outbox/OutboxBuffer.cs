using BareWire.Abstractions.Transport;

namespace BareWire.Outbox;

internal sealed class OutboxBuffer
{
    private readonly List<OutboundMessage> _messages = [];

    internal void Add(OutboundMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _messages.Add(message);
    }

    internal IReadOnlyList<OutboundMessage> GetMessages() => _messages;

    internal void Clear() => _messages.Clear();

    internal bool IsEmpty => _messages.Count == 0;
}
