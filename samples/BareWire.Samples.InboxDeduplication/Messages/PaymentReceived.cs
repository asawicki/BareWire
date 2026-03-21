namespace BareWire.Samples.InboxDeduplication.Messages;

// Past-tense event name per ADR-005 naming conventions (CONSTITUTION.md).
// Plain record — no base class, no attributes (ADR-001 raw-first).
public record PaymentReceived(string PaymentId, string Payer, string Payee, decimal Amount, DateTime PaidAt);
