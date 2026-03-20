using BareWire.Abstractions;
using BareWire.Abstractions.Serialization;
using BareWire.Transport.RabbitMQ.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace BareWire.Transport.RabbitMQ;

/// <summary>
/// RabbitMQ implementation of <see cref="IRequestClientFactory"/>.
/// Maintains a lazily-established, shared <see cref="IConnection"/> for all request clients
/// created by this factory. The connection mirrors the options and TLS settings from
/// <see cref="RabbitMqTransportOptions"/>.
/// </summary>
internal sealed partial class RabbitMqRequestClientFactory : IRequestClientFactory, IAsyncDisposable
{
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(30);

    private readonly RabbitMqTransportOptions _options;
    private readonly IMessageSerializer _serializer;
    private readonly IMessageDeserializer _deserializer;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<RabbitMqRequestClientFactory> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    private IConnection? _connection;
    private bool _disposed;

    internal RabbitMqRequestClientFactory(
        RabbitMqTransportOptions options,
        IMessageSerializer serializer,
        IMessageDeserializer deserializer,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(deserializer);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _options = options;
        _serializer = serializer;
        _deserializer = deserializer;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<RabbitMqRequestClientFactory>();
    }

    /// <inheritdoc/>
    public IRequestClient<T> CreateRequestClient<T>() where T : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Synchronously ensure the connection is open. The first call may block briefly
        // while the AMQP connection is established; subsequent calls return immediately
        // because the lock check is optimistic.
        EnsureConnectedSync();

        string routingKey = typeof(T).FullName ?? typeof(T).Name;
        ILogger clientLogger = _loggerFactory.CreateLogger<RabbitMqRequestClient<T>>();

        var client = new RabbitMqRequestClient<T>(
            connection: _connection!,
            serializer: _serializer,
            deserializer: _deserializer,
            logger: clientLogger,
            targetExchange: _options.DefaultExchange,
            routingKey: routingKey,
            timeout: DefaultRequestTimeout);

        // InitializeAsync declares the exclusive auto-delete response queue and starts the
        // consumer. We block here because IRequestClientFactory.CreateRequestClient is
        // synchronous; the blocking is bounded (single network round-trip to the broker).
        client.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();

        LogRequestClientCreated(typeof(T).Name, routingKey);
        return client;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_connection is not null)
        {
            try
            {
                await _connection.CloseAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogConnectionCloseError(ex);
            }

            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }

        _connectionLock.Dispose();
    }

    // ── Connection management ─────────────────────────────────────────────────

    private void EnsureConnectedSync()
    {
        if (_connection is not null && _connection.IsOpen)
        {
            return;
        }

        _connectionLock.Wait();
        try
        {
            // Double-check inside the lock.
            if (_connection is not null && _connection.IsOpen)
            {
                return;
            }

            _connection = CreateConnectionAsync(CancellationToken.None).GetAwaiter().GetResult();
            LogConnectionEstablished(_connection.Endpoint.HostName, _connection.Endpoint.Port);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        var uri = new Uri(_options.ConnectionString);

        var factory = new ConnectionFactory
        {
            Uri = uri,
            AutomaticRecoveryEnabled = _options.AutomaticRecoveryEnabled,
            NetworkRecoveryInterval = _options.NetworkRecoveryInterval,
        };

        if (_options.ConfigureTls is not null)
        {
            string serverName = uri.Host;
            var tlsConfigurator = new RabbitMqTlsConfigurator();
            _options.ConfigureTls(tlsConfigurator);
            factory.Ssl = tlsConfigurator.Build(serverName);
        }
        else if (_options.SslOptions is not null)
        {
            factory.Ssl = _options.SslOptions;
        }

        return await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
    }

    // ── Logger messages ───────────────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Information,
        Message = "RabbitMQ request client factory connection established to {Host}:{Port}.")]
    private partial void LogConnectionEstablished(string host, int port);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Request client created for '{MessageType}' (routingKey='{RoutingKey}').")]
    private partial void LogRequestClientCreated(string messageType, string routingKey);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Exception while closing RabbitMQ request client factory connection during dispose.")]
    private partial void LogConnectionCloseError(Exception ex);
}
