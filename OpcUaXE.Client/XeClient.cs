using OpcUaXE.Client.Core;
using OpcUaXE.Client.Core.Handlers;
using OpcUaXE.Client.Core.Helper;
using OpcUaXE.Client.Events;
using OpcUaXE.Client.Types;

namespace OpcUaXE.Client;

/// <summary>
/// High-level OPC UA client providing connect/disconnect with automatic keep-alive and
/// reconnect, read/write, subscriptions, address-space browsing, and Alarms and Conditions
/// in a single, easy-to-use class.
/// </summary>
/// <remarks>
/// <para><b>Lifecycle</b></para>
/// <list type="number">
/// <item>Construct: <c>var client = new XeClient();</c></item>
/// <item>Subscribe to events (<see cref="StateChanged"/>, <see cref="CertificateValidationRequested"/>, …).</item>
/// <item>Connect: <see cref="ConnectAsync(string,int,XeClientOptions?,bool,CancellationToken)"/>.
///     With the default <c>blocking: true</c> the call returns only after the session is established.
///     Pass <c>blocking: false</c> to return immediately and let the background loop connect.</item>
/// <item>Disconnect (optional): <see cref="DisconnectAsync"/> before disposal.</item>
/// <item>Dispose: <c>await client.DisposeAsync();</c>  (or <c>await using</c>).</item>
/// </list>
///
/// <para><b>Thread safety</b></para>
/// All public methods are thread-safe. Events are raised on internal background tasks;
/// handlers must not block for extended periods to avoid channel overflow.
///
/// <para><b>Disposal</b></para>
/// Prefer <see cref="DisposeAsync"/> so that background tasks are gracefully awaited.
/// If <see cref="IDisposable.Dispose"/> is used, background tasks receive a cancellation
/// signal but are not awaited.
///
/// <para><b>Quick start</b></para>
/// <code language="csharp">
/// await using var client = new XeClient();
///
/// client.ServiceMessage   += (_, e) => Console.WriteLine(e.Message);
/// client.ServiceException += (_, e) => Console.WriteLine(e.Exception.Message);
/// client.StateChanged     += (_, e) => Console.WriteLine(e.State);
/// client.CertificateValidationRequested += (_, e) => e.AcceptPermanently = true;
///
/// var options = new XeClientOptions
/// {
///     ClientName           = "MyApp",
///     KeepAliveTimeout     = TimeSpan.FromSeconds(30),
///     AutoReconnectDelay   = TimeSpan.FromSeconds(5)
/// };
///
/// // blocking: true (default) – returns once the OPC UA session is established.
/// await client.ConnectAsync("192.168.1.100", options: options);
///
/// client.MonitoredItemValuesReceived += (_, items) =>
/// {
///     foreach (var item in items) Console.WriteLine(item);
/// };
/// client.AddMonitoredItem("ns=2;s=MyVariable");
/// </code>
/// </remarks>
public sealed class XeClient : IXeClient, IAsyncDisposable, IDisposable
{
    private readonly XeClientContext _ctx;
    private readonly IConnectionHandler _connection;
    private readonly IReadWriteHandler _readWrite;
    private readonly ISubscriptionHandler _subscriptions;
    private readonly IAlarmsHandler _alarms;
    private readonly IBrowseHandler _browser;
    private readonly Task[] _backgroundTasks;
    private int _disposed;

    #region Constructors

    /// <summary>
    /// Initializes a new <see cref="XeClient"/> instance and starts the internal service loops.
    /// </summary>
    public XeClient() : this(
        new XeClientContext(),
        ctx => new ConnectionHandler(ctx),
        ctx => new ReadWriteHandler(ctx),
        ctx => new SubscriptionHandler(ctx),
        ctx => new AlarmsHandler(ctx),
        ctx => new BrowseHandler(ctx))
    { }

    /// <summary>
    /// Internal constructor for unit testing: accepts pre-built handler instances.
    /// </summary>
    internal XeClient(
        XeClientContext ctx,
        Func<XeClientContext, IConnectionHandler> connectionFactory,
        Func<XeClientContext, IReadWriteHandler> readWriteFactory,
        Func<XeClientContext, ISubscriptionHandler> subscriptionFactory,
        Func<XeClientContext, IAlarmsHandler> alarmsFactory,
        Func<XeClientContext, IBrowseHandler> browseFactory)
    {
        _ctx = ctx;
        _connection = connectionFactory(ctx);
        _readWrite = readWriteFactory(ctx);
        _subscriptions = subscriptionFactory(ctx);
        _alarms = alarmsFactory(ctx);
        _browser = browseFactory(ctx);

        WireEvents();
        _backgroundTasks = StartBackgroundTasks();
    }

    #endregion

    #region Public Events

    /// <inheritdoc/>
    public event EventHandler<XeClientStateChangedEventArgs>? StateChanged;

    /// <inheritdoc/>
    public event EventHandler<XeServiceMessageEventArgs>? ServiceMessage;

    /// <inheritdoc/>
    public event EventHandler<XeServiceExceptionEventArgs>? ServiceException;

    /// <inheritdoc/>
    public event EventHandler<XeCertificateValidationEventArgs>? CertificateValidationRequested;

    /// <inheritdoc/>
    public event EventHandler<XeKeepAliveEventArgs>? KeepAliveReceived;

    /// <inheritdoc/>
    public event EventHandler<IReadOnlyList<XeReadItem>>? MonitoredItemValuesReceived;

    /// <inheritdoc/>
    public event EventHandler<XeAlarmAndConditionEventArgs>? AlarmAndConditionEventReceived;

    #endregion

    #region Public Properties

    /// <inheritdoc/>
    public XeClientState State => _ctx.State;

    /// <inheritdoc/>
    public bool IsConnected => _ctx.IsConnected;

    #endregion

    #region Connection

    /// <inheritdoc/>
    public async Task<IReadOnlyList<XeConnectionEndpoint>> BrowseServerEndpointsAsync(
        string ipAddress, int port = 4840, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ipAddress);
        return await _connection.BrowseServerEndpointsAsync(ipAddress, port, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ConnectAsync(
        string ipAddress, int port = 4840, XeClientOptions? options = null,
        bool blocking = true, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ipAddress);
        await _connection.ConnectAsync(
            new XeConnectionInfo { IpAddress = ipAddress, Port = port, Blocking = blocking },
            options, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ConnectAsync(
        XeConnectionEndpoint serverEndpoint, XeClientOptions? options = null,
        bool blocking = true, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(serverEndpoint);
        await _connection.ConnectAsync(
            new XeConnectionInfo { ServerEndpoint = serverEndpoint, Blocking = blocking },
            options, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await _connection.DisconnectAsync(ct).ConfigureAwait(false);
    }

    #endregion

    #region Read / Write

    /// <inheritdoc/>
    public Task<XeReadItem> ReadValueAsync(string nodeAddress, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(nodeAddress);
        return _readWrite.ReadValueAsync(nodeAddress, ct);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<XeReadItem>> ReadValuesAsync(
        IReadOnlyList<string> nodeAddresses, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(nodeAddresses);
        return _readWrite.ReadValuesAsync(nodeAddresses, ct);
    }

    /// <inheritdoc/>
    public Task WriteValueAsync(XeWriteItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _readWrite.WriteValueAsync(item, ct);
    }

    /// <inheritdoc/>
    public Task<XeWriteItem> WriteValueAsync(
        string nodeAddress, object value, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(nodeAddress);
        ArgumentNullException.ThrowIfNull(value);
        return _readWrite.WriteValueAsync(nodeAddress, value, ct);
    }

    /// <inheritdoc/>
    public Task WriteValuesAsync(
        IReadOnlyList<XeWriteItem> items, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        return _readWrite.WriteValuesAsync(items, ct);
    }

    #endregion

    #region Subscriptions

    /// <inheritdoc/>
    public void AddMonitoredItem(
        string nodeAddress, int samplingInterval = 0, int publishInterval = 1000)
    {
        ArgumentNullException.ThrowIfNull(nodeAddress);
        _subscriptions.AddMonitoredItem(nodeAddress, samplingInterval, publishInterval);
    }

    /// <inheritdoc/>
    public void AddMonitoredItems(IReadOnlyList<XeMonitoredItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _subscriptions.AddMonitoredItems(items);
    }

    /// <inheritdoc/>
    public void RemoveMonitoredItem(string nodeAddress)
    {
        ArgumentNullException.ThrowIfNull(nodeAddress);
        _subscriptions.RemoveMonitoredItem(nodeAddress);
    }

    /// <inheritdoc/>
    public void RemoveMonitoredItems(IReadOnlyList<string> nodeAddresses)
    {
        ArgumentNullException.ThrowIfNull(nodeAddresses);
        _subscriptions.RemoveMonitoredItems(nodeAddresses);
    }

    /// <inheritdoc/>
    public void RemoveAllMonitoredItems() =>
        _subscriptions.RemoveAllMonitoredItems();

    #endregion

    #region Browser

    /// <inheritdoc/>
    public Task<IReadOnlyList<XeBrowseResultItem>> BrowseRootAsync(CancellationToken ct = default) =>
        _browser.BrowseRootAsync(ct);

    /// <inheritdoc/>
    public Task<IReadOnlyList<XeBrowseResultItem>> BrowseAsync(
        string nodeAddress, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(nodeAddress);
        return _browser.BrowseAsync(nodeAddress, ct);
    }

    #endregion

    #region Alarms and Conditions

    /// <inheritdoc/>
    public Task RequestAlarmConditionRefreshAsync(CancellationToken ct = default) =>
        _alarms.RequestConditionRefreshAsync(ct);

    #endregion

    #region Disposal

    /// <summary>
    /// Asynchronously disposes the client by gracefully stopping all background tasks
    /// and releasing all resources. Safe to call multiple times.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _ctx.Cancel();
        await Task.WhenAll(_backgroundTasks.Select(SafeAwait)).ConfigureAwait(false);
        _ctx.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Synchronously signals all background tasks to stop without awaiting them.
    /// Prefer <see cref="DisposeAsync"/> to ensure graceful shutdown.
    /// Safe to call multiple times.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _ctx.Cancel();
        _ctx.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Private Helpers

    private void WireEvents()
    {
        // Context infrastructure events
        _ctx.StateChanged += (_, e) => StateChanged?.Invoke(this, e);
        _ctx.ServiceMessage += (_, e) => ServiceMessage?.Invoke(this, e);
        _ctx.ServiceException += (_, e) => ServiceException?.Invoke(this, e);

        // Handler domain events
        _connection.CertificateValidationRequested +=
            (_, e) => CertificateValidationRequested?.Invoke(this, e);
        _connection.KeepAliveReceived +=
            (_, e) => KeepAliveReceived?.Invoke(this, e);
        _subscriptions.MonitoredItemValuesReceived +=
            (_, e) => MonitoredItemValuesReceived?.Invoke(this, e);
        _alarms.AlarmAndConditionEventReceived +=
            (_, e) => AlarmAndConditionEventReceived?.Invoke(this, e);
    }

    private Task[] StartBackgroundTasks() =>
    [
        Task.Run(_connection.ConnectionLoopAsync),
        Task.Run(_subscriptions.SyncLoopAsync),
        Task.Run(_subscriptions.ConsumerLoopAsync),
        Task.Run(_alarms.AlarmsLoopAsync),
        Task.Run(_alarms.NotificationLoopAsync),
    ];

    private static async Task SafeAwait(Task t)
    {
        try { await t.ConfigureAwait(false); }
        catch { /* cancellation and cleanup exceptions are expected on shutdown */ }
    }

    #endregion
}
