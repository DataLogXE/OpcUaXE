using OpcUaXE.Client.Types;

namespace OpcUaXE.Client.Core.Helper;

/// <summary>Internal connection parameters passed from <see cref="XeClient"/> to <c>IConnectionHandler</c>.</summary>
internal sealed class XeConnectionInfo
{
    /// <summary>Server IP address.</summary>
    internal string IpAddress { get; set; } = string.Empty;

    /// <summary>Server TCP port (0 = not set).</summary>
    internal int Port { get; set; }

    /// <summary>Pre-selected server endpoint; <see langword="null"/> triggers auto-discovery.</summary>
    internal XeConnectionEndpoint? ServerEndpoint { get; set; }

    /// <summary>
    /// When <see langword="true"/>, <c>IConnectionHandler.ConnectAsync</c> blocks until
    /// the session is established. When <see langword="false"/>, it returns immediately and the
    /// background loop connects asynchronously.
    /// </summary>
    internal bool Blocking { get; set; }
}
