namespace OpcUaXE.Client.Types;

/// <summary>
/// Represents the configuration for a single OPC UA node to be monitored,
/// including its address and the desired sampling and publish intervals.
/// </summary>
public sealed class XeMonitoredItem
{
    /// <summary>
    /// Gets the node address associated with this node.
    /// </summary>
    public XeNodeAddress Address { get; }

    /// <summary>
    /// Gets the sampling interval, in milliseconds, used for periodic data collection.
    /// </summary>
    public int SamplingIntervalMs { get; } = 0;

    /// <summary>
    /// Gets the interval, in milliseconds, between consecutive publish operations.
    /// </summary>
    public int PublishIntervalMs { get; } = 0;

    /// <summary>Initializes a new monitored item for the given node.</summary>
    /// <param name="nodeAddress">OPC UA node address string.</param>
    /// <param name="samplingIntervalMs">Sampling interval in ms; 0 = server minimum.</param>
    /// <param name="publishIntervalMs">Publish interval in ms (default 1000).</param>
    public XeMonitoredItem(string nodeAddress, int samplingIntervalMs = 0, int publishIntervalMs = 1000)
    {
        Address = new XeNodeAddress(nodeAddress);
        SamplingIntervalMs = samplingIntervalMs;
        PublishIntervalMs = publishIntervalMs;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"Address={Address}; SamplingIntervalMs={SamplingIntervalMs}; PublishIntervalMs={PublishIntervalMs}; ";
    }

}
