namespace OpcUaXE.Client.Events;

/// <summary>Event data for an OPC UA keep-alive notification.</summary>
public sealed class XeKeepAliveEventArgs : EventArgs
{
    /// <summary>Server time reported in the keep-alive response (UTC).</summary>
    public DateTime CurrentTimeUtc { get; }

    internal XeKeepAliveEventArgs(DateTime currentTimeUtc)
    {
        CurrentTimeUtc = currentTimeUtc;
    }
}
