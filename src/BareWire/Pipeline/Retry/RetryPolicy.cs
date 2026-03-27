namespace BareWire.Pipeline.Retry;

internal abstract class RetryPolicy
{
    private readonly TimeProvider _timeProvider;

    internal int MaxRetries { get; }
    internal IReadOnlyList<Type> HandledExceptions { get; }
    internal IReadOnlyList<Type> IgnoredExceptions { get; }

    protected RetryPolicy(
        int maxRetries,
        IReadOnlyList<Type> handledExceptions,
        IReadOnlyList<Type> ignoredExceptions,
        TimeProvider? timeProvider = null)
    {
        if (maxRetries < 0)
            throw new ArgumentOutOfRangeException(nameof(maxRetries), "MaxRetries must be non-negative.");

        MaxRetries = maxRetries;
        HandledExceptions = handledExceptions ?? throw new ArgumentNullException(nameof(handledExceptions));
        IgnoredExceptions = ignoredExceptions ?? throw new ArgumentNullException(nameof(ignoredExceptions));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    internal abstract TimeSpan GetDelay(int attempt);

    internal bool ShouldRetry(Exception ex, int attempt)
    {
        ArgumentNullException.ThrowIfNull(ex);

        if (attempt >= MaxRetries)
            return false;

        // Check ignored list first — ignored always wins
        foreach (Type ignoredType in IgnoredExceptions)
        {
            if (ignoredType.IsInstanceOfType(ex))
                return false;
        }

        // If handled list is non-empty, exception must match one of them
        if (HandledExceptions.Count > 0)
        {
            foreach (Type handledType in HandledExceptions)
            {
                if (handledType.IsInstanceOfType(ex))
                    return true;
            }

            return false;
        }

        // No filter — retry all non-ignored exceptions
        return true;
    }

    internal Task DelayAsync(int attempt, CancellationToken ct)
    {
        TimeSpan delay = GetDelay(attempt);
        return Task.Delay(delay, _timeProvider, ct);
    }
}
