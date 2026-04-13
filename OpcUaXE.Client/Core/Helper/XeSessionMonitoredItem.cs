using Opc.Ua.Client;

namespace OpcUaXE.Client.Core.Helper;

internal class XeSessionMonitoredItem
{
    /// <summary>Gets the underlying SDK <see cref="MonitoredItem"/>.</summary>
    public MonitoredItem MonitoredItem { get; }

    /// <summary>Gets the node id string of the monitored item.</summary>
    public string NodeId { get => $"{MonitoredItem.StartNodeId}"; }

    /// <summary>
    /// Initializes a new instance with the given <paramref name="monitoredItem"/>.
    /// </summary>
    /// <param name="monitoredItem">SDK monitored item to wrap.</param>
    public XeSessionMonitoredItem(MonitoredItem monitoredItem)
    {
        MonitoredItem = monitoredItem;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"NodeId={NodeId}; Subscription={MonitoredItem.Subscription?.DisplayName}; ";
    }

}
