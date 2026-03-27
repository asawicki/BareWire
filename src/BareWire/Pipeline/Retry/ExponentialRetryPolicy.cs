namespace BareWire.Pipeline.Retry;

internal sealed class ExponentialRetryPolicy : RetryPolicy
{
    private readonly TimeSpan _minInterval;
    private readonly TimeSpan _maxInterval;

    internal ExponentialRetryPolicy(
        int maxRetries,
        TimeSpan minInterval,
        TimeSpan maxInterval,
        IReadOnlyList<Type> handledExceptions,
        IReadOnlyList<Type> ignoredExceptions,
        TimeProvider? timeProvider = null)
        : base(maxRetries, handledExceptions, ignoredExceptions, timeProvider)
    {
        if (minInterval < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(minInterval), "Minimum interval must be non-negative.");

        if (maxInterval < minInterval)
            throw new ArgumentOutOfRangeException(nameof(maxInterval), "Maximum interval must be >= minimum interval.");

        _minInterval = minInterval;
        _maxInterval = maxInterval;
    }

    internal override TimeSpan GetDelay(int attempt)
    {
        // Exponential: min * 2^attempt, capped at max, with jitter ±10%
        double baseMs = _minInterval.TotalMilliseconds * Math.Pow(2, attempt);
        double cappedMs = Math.Min(baseMs, _maxInterval.TotalMilliseconds);

        // Add ±10% jitter to spread out concurrent retries
        double jitterFactor = 1.0 + (Random.Shared.NextDouble() - 0.5) * 0.2;
        double finalMs = cappedMs * jitterFactor;

        // Clamp to [minInterval, maxInterval]
        finalMs = Math.Max(_minInterval.TotalMilliseconds, Math.Min(finalMs, _maxInterval.TotalMilliseconds));

        return TimeSpan.FromMilliseconds(finalMs);
    }
}
