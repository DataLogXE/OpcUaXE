using Opc.Ua;
using OpcUaXE.Client.Core.Helper;
using OpcUaXE.Client.Exceptions;
using OpcUaXE.Client.Types;

namespace OpcUaXE.Client.Core.Handlers;

/// <summary>
/// Provides read/write operations for OPC UA node values,
/// including automatic type coercion from the node's declared data type.
/// </summary>
internal sealed class ReadWriteHandler : IReadWriteHandler
{
    private readonly XeClientContext _ctx;

    public ReadWriteHandler(XeClientContext ctx) => _ctx = ctx;

    #region Public API

    public async Task<XeReadItem> ReadValueAsync(string nodeAddress, CancellationToken ct)
    {
        return (await ReadValuesInternalAsync([nodeAddress], ct).ConfigureAwait(false)).First();
    }

    public async Task<IReadOnlyList<XeReadItem>> ReadValuesAsync(
        IReadOnlyList<string> nodeAddresses, CancellationToken ct)
    {
        return await ReadValuesInternalAsync(nodeAddresses, ct).ConfigureAwait(false);
    }

    public async Task WriteValueAsync(XeWriteItem item, CancellationToken ct)
    {
        await WriteValuesInternalAsync([item], ct).ConfigureAwait(false);
    }

    public async Task<XeWriteItem> WriteValueAsync(
        string nodeAddress, object value, CancellationToken ct)
    {
        XeWriteItem writeItem = new(nodeAddress, value);
        await WriteValuesInternalAsync([writeItem], ct).ConfigureAwait(false);
        return writeItem;
    }

    public async Task WriteValuesAsync(IReadOnlyList<XeWriteItem> items, CancellationToken ct)
    {
        await WriteValuesInternalAsync(items, ct).ConfigureAwait(false);
    }

    #endregion

    #region Private Implementation

    private void ThrowIfNotConnected()
    {
        if (!_ctx.IsConnected)
            throw new XeConnectionException("Client is not connected.");
    }

    private async Task<List<XeReadItem>> ReadValuesInternalAsync(
        IReadOnlyList<string> nodeAddresses, CancellationToken ct)
    {
        ThrowIfNotConnected();

        List<XeReadItem> results = [];
        ReadValueIdCollection nodesToRead = [];

        if (nodeAddresses.Count == 0)
            return results;

        foreach (string addr in nodeAddresses)
        {
            XeNodeAddress address = new(addr, _ctx.Session!);
            results.Add(new XeReadItem(address));
            nodesToRead.Add(new ReadValueId
            {
                NodeId = address.NodeId,
                AttributeId = Attributes.Value
            });
        }

        ReadResponse readResponse = await _ctx.Session!.ReadAsync(
            requestHeader: null,
            maxAge: 0,
            timestampsToReturn: TimestampsToReturn.Both,
            nodesToRead: nodesToRead,
            ct: ct).ConfigureAwait(false);

        for (int i = 0; i < results.Count; i++)
            results[i].DataValue = readResponse.Results[i];

        return results;
    }

    private async Task WriteValuesInternalAsync(
        IReadOnlyList<XeWriteItem> items, CancellationToken ct)
    {
        ThrowIfNotConnected();

        WriteValueCollection nodesToWrite =
            await PrepareNodesToWriteAsync(items, ct).ConfigureAwait(false);

        WriteResponse writeResult = await _ctx.Session!.WriteAsync(
            requestHeader: null,
            nodesToWrite: nodesToWrite,
            ct: ct).ConfigureAwait(false);

        for (int i = 0; i < items.Count; i++)
            items[i].State = new XeValueState(writeResult.Results[i]);
    }

    private async Task<WriteValueCollection> PrepareNodesToWriteAsync(
        IReadOnlyList<XeWriteItem> items, CancellationToken ct)
    {
        WriteValueCollection nodesToWrite = [];

        foreach (XeWriteItem item in items)
        {
            item.Address.CheckValid(_ctx.Session!);
            object? castValue = item.Value;

            try
            {
                NodeId? nodeId = item.Address.NodeId;
                INode? node = await _ctx.Session!.NodeCache.FindAsync(nodeId!).ConfigureAwait(false);
                if (node is VariableNode variable)
                {
                    BuiltInType builtIn = TypeInfo.GetBuiltInType(variable.DataType);
                    if (builtIn != BuiltInType.Null)
                        castValue = TypeInfo.Cast(item.Value, builtIn);
                }
            }
            catch (Exception ex)
            {
                // Type coercion is best-effort; write with the original value on failure.
                _ctx.RaiseServiceException(new InvalidOperationException(
                    $"Type coercion failed for node '{item.Address.NodeAddress}', " +
                    $"using original value. Inner: {ex.Message}", ex));
            }

            nodesToWrite.Add(new WriteValue
            {
                NodeId = item.Address.NodeId!,
                AttributeId = Attributes.Value,
                Value = new DataValue(new Variant(castValue))
            });
        }

        return nodesToWrite;
    }

    #endregion
}
