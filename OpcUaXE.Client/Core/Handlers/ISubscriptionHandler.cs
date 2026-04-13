using OpcUaXE.Client.Types;

namespace OpcUaXE.Client.Core.Handlers;

/// <summary>Manages OPC UA monitored items and value-change subscriptions.</summary>
internal interface ISubscriptionHandler
{
    /// <summary>Raised when new values are received for one or more monitored items.</summary>
    event EventHandler<IReadOnlyList<XeReadItem>>? MonitoredItemValuesReceived;

    /// <summary>Adds a monitored item for the given node address.</summary>
    void AddMonitoredItem(string nodeAddress, int samplingIntervalMs, int publishIntervalMs);

    /// <summary>Adds monitored items; duplicates are silently ignored.</summary>
    void AddMonitoredItems(IReadOnlyList<XeMonitoredItem> items);

    /// <summary>Removes the monitored item for the given node address. No-op if not found.</summary>
    void RemoveMonitoredItem(string nodeAddress);

    /// <summary>Removes monitored items for the given node addresses.</summary>
    void RemoveMonitoredItems(IReadOnlyList<string> nodeAddresses);

    /// <summary>Removes all monitored items.</summary>
    void RemoveAllMonitoredItems();

    /// <summary>Background loop that reconciles the client item list with the OPC UA session.</summary>
    Task SyncLoopAsync();

    /// <summary>Background loop that drains the value channel and raises the received event.</summary>
    Task ConsumerLoopAsync();
}
