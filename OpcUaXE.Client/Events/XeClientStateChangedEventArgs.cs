namespace OpcUaXE.Client.Events;

/// <summary>
/// Event data raised when the <see cref="XeClient"/> connection state changes.
/// </summary>
public sealed class XeClientStateChangedEventArgs : EventArgs
{
    /// <summary>Gets the new connection state.</summary>
    public XeClientState State { get; }

    internal XeClientStateChangedEventArgs(XeClientState state)
    {
        State = state;
    }
}
