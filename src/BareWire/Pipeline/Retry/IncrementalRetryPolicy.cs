namespace BareWire.Pipeline.Retry;

internal sealed class IncrementalRetryPolicy : RetryPolicy
{
    private readonly TimeSpan _initial;
    private readonly TimeSpan _increment;

    internal IncrementalRetryPolicy(
        int maxRetries,
        TimeSpan initial,
        TimeSpan increment,
        IReadOnlyList<Type> handledExceptions,
        IReadOnlyList<Type> ignoredExceptions,
        TimeProvider? timeProvider = null)
        : base(maxRetries, handledExceptions, ignoredExceptions, timeProvider)
    {
        if (initial < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(initial), "Initial delay must be non-negative.");

        _initial = initial;
        _increment = increment;
    }

    internal override TimeSpan GetDelay(int attempt)
    {
        // attempt is 0-based: first retry uses attempt=0
        long ticks = _initial.Ticks + _increment.Ticks * attempt;

        // Guard against overflow or negative totals from negative increment
        if (ticks < 0)
            return TimeSpan.Zero;

        return TimeSpan.FromTicks(ticks);
    }
}
