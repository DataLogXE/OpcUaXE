using OpcUaXE.Client.Core.Helper;
using OpcUaXE.Client.Events;
using OpcUaXE.Client.Types;

namespace OpcUaXE.Client.Core.Handlers;

/// <summary>
/// Manages the OPC UA session lifecycle: endpoint discovery, connect, disconnect,
/// keep-alive and auto-reconnect.
/// </summary>
internal interface IConnectionHandler
{
    /// <summary>Raised when the server certificate requires validation.</summary>
    event EventHandler<XeCertificateValidationEventArgs>? CertificateValidationRequested;

    /// <summary>Raised when a keep-alive notification is received.</summary>
    event EventHandler<XeKeepAliveEventArgs>? KeepAliveReceived;

    /// <summary>
    /// Initiates a connection attempt. When <see cref="XeConnectionInfo.Blocking"/> is
    /// <see langword="true"/>, blocks until the session is established.
    /// </summary>
    Task ConnectAsync(XeConnectionInfo connectionInfo, XeClientOptions? options, CancellationToken ct);

    /// <summary>Signals the connection loop to stop and optionally waits for disconnect.</summary>
    Task DisconnectAsync(CancellationToken ct);

    /// <summary>Returns available server endpoints from the given address and port.</summary>
    Task<IReadOnlyList<XeConnectionEndpoint>> BrowseServerEndpointsAsync(
        string ipAddress, int port, CancellationToken ct);

    /// <summary>Long-running background loop that maintains the session lifecycle.</summary>
    Task ConnectionLoopAsync();
}
