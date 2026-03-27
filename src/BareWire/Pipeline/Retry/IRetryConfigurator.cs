namespace BareWire.Pipeline.Retry;

internal interface IRetryConfigurator
{
    IRetryConfigurator Interval(int retryCount, TimeSpan interval);
    IRetryConfigurator Incremental(int retryCount, TimeSpan initial, TimeSpan increment);
    IRetryConfigurator Exponential(int retryCount, TimeSpan minInterval, TimeSpan maxInterval);
    IRetryConfigurator Handle<TException>() where TException : Exception;
    IRetryConfigurator Ignore<TException>() where TException : Exception;

    RetryPolicy Build();
}
