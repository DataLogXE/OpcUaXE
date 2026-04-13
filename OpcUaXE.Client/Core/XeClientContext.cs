using Opc.Ua.Client;
using OpcUaXE.Client.Events;
using System.Diagnostics;

namespace OpcUaXE.Client.Core;

/// <summary>
/// Internal shared-state kernel passed to every handler.
/// Single source of truth for session, connection state, options and lifetime.
/// </summary>
internal sealed class XeClientContext : IDisposable
{
    private readonly CancellationTokenSource _lifetimeCts = new();
    private volatile XeClientState _state = XeClientState.Disconnected;
    private XeClientOptions _options = new();

    // Single writer: ConnectionHandler. Multiple readers: all other handlers.
    private volatile ISession? _session;

    public ISession? Session
    {
        get => _session;
        internal set => _session = value;
    }

    public CancellationToken LifetimeToken => _lifetimeCts.Token;
    public XeClientState State => _state;
    public bool IsConnected => _state == XeClientState.Connected;
    public XeClientOptions Options => _options;

    // Display name of the A&C OPC UA subscription — shared across handlers.
    internal const string AcSubscriptionName = "OpcUaXE_AlarmsAndConditions";

    // EVENTS — raised by handlers, wired up to XeClient public events in the facade.

    public event EventHandler<XeClientStateChangedEventArgs>? StateChanged;
    public event EventHandler<XeServiceMessageEventArgs>? ServiceMessage;
    public event EventHandler<XeServiceExceptionEventArgs>? ServiceException;

    #region Options / State Mutations

    public void SetOptions(XeClientOptions options) => _options = options;

    public void SetState(XeClientState state)
    {
        _state = state;
        StateChanged?.Invoke(this, new XeClientStateChangedEventArgs(state));
    }

    #endregion

    #region Event Raising

    public void RaiseServiceMessage(string message) =>
        ServiceMessage?.Invoke(this, new XeServiceMessageEventArgs(message));

    public void RaiseServiceException(Exception ex) =>
        ServiceException?.Invoke(this, new XeServiceExceptionEventArgs(ex));

    #endregion

    #region Utility

    /// <summary>Returns elapsed time since <paramref name="startTimestamp"/> (monotonic, DST-safe).</summary>
    public static TimeSpan GetElapsed(long startTimestamp) =>
        Stopwatch.GetElapsedTime(startTimestamp);

    #endregion

    #region Lifetime

    /// <summary>Signals all background tasks to stop by cancelling the lifetime token.</summary>
    public void Cancel() => _lifetimeCts.Cancel();

    public void Dispose()
    {
        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();
    }

    #endregion
}
