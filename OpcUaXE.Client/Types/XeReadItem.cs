using Opc.Ua;
using OpcUaXE.Client.Core.Helper;

namespace OpcUaXE.Client.Types;

/// <summary>
/// Result of a single OPC UA node read: address, value, server timestamp, and status.
/// </summary>
public sealed class XeReadItem
{
    /// <summary>Gets the OPC UA node address associated with this read result.</summary>
    public XeNodeAddress Address { get; }

    /// <summary>
    /// Gets the value wrapped for ergonomic implicit conversion to common scalar types.
    /// </summary>
    public XeValueWrapper Value => new(_dataValue?.Value);

    /// <summary>
    /// Flat enumeration of a complex or array value; <see langword="null"/> for scalar types.
    /// </summary>
    public XeComplexValueCollection? ValueCollection => GetComplexValueCollection();

    /// <summary>Server-side timestamp of the value (UTC).</summary>
    public DateTime ServerTimeUtc => _dataValue?.ServerTimestamp ?? DateTime.MinValue;

    /// <summary>Gets the OPC UA quality/status of this read result.</summary>
    public XeValueState State => _state ??= BuildState();

    internal DataValue? DataValue
    {
        get => _dataValue;
        set
        {
            _dataValue = value;
            _state = null; // invalidate cached state when data changes
        }
    }

    private DataValue? _dataValue;
    private XeValueState? _state;
    private XeComplexValueCollection? _complexValueCache;

    internal XeReadItem(XeNodeAddress nodeAddress, DataValue? dataValue = null)
    {
        Address = nodeAddress;
        _dataValue = dataValue;
    }

    /// <inheritdoc />
    public override string ToString() =>
        $"Address={Address}; " +
        $"ServerTimeUtc={ServerTimeUtc:yyyy-MM-ddTHH:mm:ss.ffffffZ}; " +
        $"State={State}; " +
        $"Value={Value}; ";

    private XeValueState BuildState() =>
        _dataValue is null
            ? new XeValueState(StatusCodes.BadUnexpectedError)
            : new XeValueState(_dataValue.StatusCode);

    private XeComplexValueCollection? GetComplexValueCollection()
    {
        if (_complexValueCache == null && _dataValue != null)
        {
            if (_dataValue.Value is ExtensionObject extensionObject)
                _complexValueCache = extensionObject.Enumerate();
            else if (_dataValue.Value is Array array)
                _complexValueCache = array.Enumerate();
        }
        return _complexValueCache;
    }
}
