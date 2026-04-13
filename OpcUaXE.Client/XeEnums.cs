namespace OpcUaXE.Client;

/// <summary>Represents the connection state of an <see cref="XeClient"/> instance.</summary>
public enum XeClientState
{
    /// <summary>The client is establishing a connection to the server.</summary>
    Connecting,

    /// <summary>The client is connected and the session is active.</summary>
    Connected,

    /// <summary>An error occurred; the session may be lost. An automatic reconnect may follow.</summary>
    Error,

    /// <summary>The client is in the process of disconnecting.</summary>
    Disconnecting,

    /// <summary>The client is disconnected from the server.</summary>
    Disconnected
}
