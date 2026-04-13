using OpcUaXE.Client.Types;

namespace OpcUaXE.Client.Core.Handlers;

/// <summary>Executes OPC UA read and write operations against the active session.</summary>
internal interface IReadWriteHandler
{
    /// <summary>Reads the current value of a single node.</summary>
    Task<XeReadItem> ReadValueAsync(string nodeAddress, CancellationToken ct);

    /// <summary>Reads values from multiple nodes in a single server round-trip.</summary>
    Task<IReadOnlyList<XeReadItem>> ReadValuesAsync(
        IReadOnlyList<string> nodeAddresses, CancellationToken ct);

    /// <summary>Writes a single value to the specified node.</summary>
    Task WriteValueAsync(XeWriteItem item, CancellationToken ct);

    /// <summary>Writes a value to the specified node address and returns the populated write item.</summary>
    Task<XeWriteItem> WriteValueAsync(string nodeAddress, object value, CancellationToken ct);

    /// <summary>Writes multiple values in a single server round-trip.</summary>
    Task WriteValuesAsync(IReadOnlyList<XeWriteItem> items, CancellationToken ct);
}
