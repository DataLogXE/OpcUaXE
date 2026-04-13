namespace OpcUaXE.Client.Events;

/// <summary>
/// Event data raised when an unhandled internal exception is caught by the OPC UA client.
/// </summary>
public sealed class XeServiceExceptionEventArgs : EventArgs
{
    /// <summary>Gets the exception that was caught internally.</summary>
    public Exception Exception { get; }

    /// <summary>Gets the UTC timestamp when the exception occurred.</summary>
    public DateTime TimestampUtc { get; }

    internal XeServiceExceptionEventArgs(Exception exception)
    {
        Exception = exception;
        TimestampUtc = DateTime.UtcNow;
    }

    /// <inheritdoc />
    public override string ToString() => $"[{TimestampUtc:HH:mm:ss.fff}Z] {Exception.GetType().Name}: {Exception.Message}";
}
