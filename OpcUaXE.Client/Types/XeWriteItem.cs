namespace OpcUaXE.Client.Types;

/// <summary>
/// Represents a value to be written to an OPC UA node.
/// After a successful write call, <see cref="State"/> contains the server's response status.
/// </summary>
public sealed class XeWriteItem
{
    /// <summary>Gets the target OPC UA node address.</summary>
    public XeNodeAddress Address { get; }

    /// <summary>Gets the value to write to the node.</summary>
    public object? Value { get; }

    /// <summary>
    /// Gets the write result status after a <see cref="OpcUaXE.Client.XeClient"/> write call completes.
    /// </summary>
    public XeValueState State
    {
        get => _state;
        internal set => _state = value;
    }

    private XeValueState _state = new();

    /// <summary>Initializes with the target node address and value to write.</summary>
    /// <param name="nodeAddress">OPC UA node address string (e.g. <c>"ns=2;s=MyNode"</c>).</param>
    /// <param name="value">Value to write. Must be compatible with the node's declared data type.</param>
    public XeWriteItem(string nodeAddress, object? value)
    {
        Address = new XeNodeAddress(nodeAddress);
        Value = value;
    }

    /// <inheritdoc />
    public override string ToString() =>
        $"Address={Address}; Value={Value}; State={State}; ";
}
