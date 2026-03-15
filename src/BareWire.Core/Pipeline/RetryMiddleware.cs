using BareWire.Abstractions.Pipeline;
using BareWire.Core.Pipeline.Retry;
using Microsoft.Extensions.Logging;

namespace BareWire.Core.Pipeline;

internal sealed class RetryMiddleware : IMessageMiddleware
{
    private readonly RetryPolicy _policy;
    private readonly ILogger<RetryMiddleware> _logger;

    internal RetryMiddleware(RetryPolicy policy, ILogger<RetryMiddleware> logger)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(MessageContext context, NextMiddleware nextMiddleware)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextMiddleware);

        int attempt = 0;

        while (true)
        {
            try
            {
                await nextMiddleware(context).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException)
            {
                // Never retry cancellation — propagate immediately
                throw;
            }
            catch (Exception ex) when (_policy.ShouldRetry(ex, attempt))
            {
                TimeSpan delay = _policy.GetDelay(attempt);
                RetryMiddlewareLogMessages.RetryingMessage(
                    _logger, context.MessageId, attempt + 1, _policy.MaxRetries, delay, ex.GetType().Name);

                await _policy.DelayAsync(attempt, context.CancellationToken).ConfigureAwait(false);

                attempt++;
            }
            catch (Exception ex)
            {
                RetryMiddlewareLogMessages.RetriesExhausted(
                    _logger, context.MessageId, attempt, ex.GetType().Name);
                throw;
            }
        }
    }
}

internal static partial class RetryMiddlewareLogMessages
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Retrying message {MessageId} (attempt {Attempt}/{MaxRetries}) after {Delay} due to {ExceptionType}")]
    internal static partial void RetryingMessage(
        ILogger logger,
        Guid messageId,
        int attempt,
        int maxRetries,
        TimeSpan delay,
        string exceptionType);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Message {MessageId} failed after {AttemptCount} attempt(s) with {ExceptionType}; retries exhausted")]
    internal static partial void RetriesExhausted(
        ILogger logger,
        Guid messageId,
        int attemptCount,
        string exceptionType);
}
