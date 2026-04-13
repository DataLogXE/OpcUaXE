using OpcUaXE.Client.Events;
using OpcUaXE.Client.Types;

namespace OpcUaXE.Client;

/// <summary>
/// Abstraction of <see cref="XeClient"/> for dependency injection and test doubles.
/// </summary>
public interface IXeClient : IAsyncDisposable, IDisposable
{
    #region Properties

    /// <summary>Current client connection state.</summary>
    XeClientState State { get; }

    /// <summary><see langword="true"/> when the session is active and connected.</summary>
    bool IsConnected { get; }

    #endregion

    #region Events

    /// <summary>Raised when an informational message is generated internally by the client.</summary>
    event EventHandler<XeServiceMessageEventArgs>? ServiceMessage;

    /// <summary>Raised when an unhandled internal exception is caught by the client.</summary>
    event EventHandler<XeServiceExceptionEventArgs>? ServiceException;

    /// <summary>Raised when the client connection state changes.</summary>
    event EventHandler<XeClientStateChangedEventArgs>? StateChanged;

    /// <summary>Raised when the server certificate requires validation.</summary>
    event EventHandler<XeCertificateValidationEventArgs>? CertificateValidationRequested;

    /// <summary>Raised when a keep-alive notification is received from the OPC UA session.</summary>
    event EventHandler<XeKeepAliveEventArgs>? KeepAliveReceived;

    /// <summary>Raised when new values are received for one or more monitored items.</summary>
    event EventHandler<IReadOnlyList<XeReadItem>>? MonitoredItemValuesReceived;

    /// <summary>Raised when an OPC UA Alarms and Conditions event is received.</summary>
    event EventHandler<XeAlarmAndConditionEventArgs>? AlarmAndConditionEventReceived;

    #endregion

    #region Connection

    /// <summary>Returns available server endpoints from the given address and port.</summary>
    Task<IReadOnlyList<XeConnectionEndpoint>> BrowseServerEndpointsAsync(
        string ipAddress, int port = 4840, CancellationToken ct = default);

    /// <summary>
    /// Connects to an OPC UA server by IP address and port.
    /// With the default <c>blocking: true</c> the call blocks until the session is established.
    /// Pass <c>blocking: false</c> to return immediately and let the background loop connect.
    /// </summary>
    Task ConnectAsync(
        string ipAddress, int port = 4840, XeClientOptions? options = null,
        bool blocking = true, CancellationToken ct = default);

    /// <summary>
    /// Connects to the specified OPC UA endpoint.
    /// With the default <c>blocking: true</c> the call blocks until the session is established.
    /// Pass <c>blocking: false</c> to return immediately and let the background loop connect.
    /// </summary>
    Task ConnectAsync(
        XeConnectionEndpoint serverEndpoint, XeClientOptions? options = null,
        bool blocking = true, CancellationToken ct = default);

    /// <summary>Disconnects from the OPC UA server.</summary>
    Task DisconnectAsync(CancellationToken ct = default);

    #endregion

    #region Read / Write

    /// <summary>Reads the current value of a single node.</summary>
    Task<XeReadItem> ReadValueAsync(string nodeAddress, CancellationToken ct = default);

    /// <summary>Reads values from multiple nodes in a single server round-trip.</summary>
    Task<IReadOnlyList<XeReadItem>> ReadValuesAsync(
        IReadOnlyList<string> nodeAddresses, CancellationToken ct = default);

    /// <summary>Writes a single value to the specified node.</summary>
    Task WriteValueAsync(XeWriteItem item, CancellationToken ct = default);

    /// <summary>Writes a value to the specified node address.</summary>
    Task<XeWriteItem> WriteValueAsync(
        string nodeAddress, object value, CancellationToken ct = default);

    /// <summary>Writes multiple values in a single OPC UA server round-trip.</summary>
    Task WriteValuesAsync(IReadOnlyList<XeWriteItem> items, CancellationToken ct = default);

    #endregion

    #region Subscriptions

    /// <summary>Adds a monitored item for the given node address.</summary>
    void AddMonitoredItem(string nodeAddress, int samplingInterval = 0, int publishInterval = 1000);

    /// <summary>Adds monitored items; duplicates are silently ignored.</summary>
    void AddMonitoredItems(IReadOnlyList<XeMonitoredItem> items);

    /// <summary>Removes the monitored item for the given node address.</summary>
    void RemoveMonitoredItem(string nodeAddress);

    /// <summary>Removes monitored items for the given node addresses.</summary>
    void RemoveMonitoredItems(IReadOnlyList<string> nodeAddresses);

    /// <summary>Removes all monitored items.</summary>
    void RemoveAllMonitoredItems();

    #endregion

    #region Browser

    /// <summary>Browses the direct children of the OPC UA root node (<c>i=84</c>).</summary>
    Task<IReadOnlyList<XeBrowseResultItem>> BrowseRootAsync(CancellationToken ct = default);

    /// <summary>Browses the child nodes of the node identified by the given address string.</summary>
    Task<IReadOnlyList<XeBrowseResultItem>> BrowseAsync(
        string nodeAddress, CancellationToken ct = default);

    #endregion

    #region Alarms and Conditions

    /// <summary>
    /// Triggers an OPC UA <c>ConditionType.ConditionRefresh</c> for the active A&amp;C subscription.
    /// All currently retained conditions are re-sent by the server.
    /// Has no effect when no A&amp;C subscription is registered.
    /// </summary>
    Task RequestAlarmConditionRefreshAsync(CancellationToken ct = default);

    #endregion
}
