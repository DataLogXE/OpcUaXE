using Opc.Ua;
using Opc.Ua.Client;

namespace OpcUaXE.Client.Types;

/// <summary>
/// Represents an OPC UA node address, resolving both <c>ns=</c>/<c>i=</c> and <c>nsu=</c> forms.
/// Validation is performed against the session namespace table when a session is provided.
/// </summary>
public sealed class XeNodeAddress
{
    /// <summary>Original node address string as supplied by the caller.</summary>
    public string NodeAddress { get; }

    /// <summary><see langword="true"/> when the address is resolved and valid within the session.</summary>
    public bool IsValid => CheckIsValid();

    /// <summary>
    /// Gets the resolved node identifier as a string (e.g. <c>"ns=2;s=MyNode"</c>),
    /// or <see cref="string.Empty"/> if not yet validated.
    /// </summary>
    public string NodeIdString => NodeId?.ToString() ?? string.Empty;

    // Kept internal to avoid leaking OPC UA SDK types into the public API surface.
    internal NodeId? NodeId { get; private set; }
    internal ExpandedNodeId? ExpandedNodeId { get; private set; }

    /// <summary>Initializes with the node address string.</summary>
    /// <param name="nodeAddress">OPC UA node address string (e.g. <c>"ns=2;s=MyNode"</c>).</param>
    public XeNodeAddress(string nodeAddress)
    {
        NodeAddress = nodeAddress;
    }

    /// <summary>Initializes with the node address and immediately validates against the session namespace table.</summary>
    internal XeNodeAddress(string nodeAddress, ISession? session)
    {
        NodeAddress = nodeAddress;
        if (session != null)
            CheckValid(session);
    }

    /// <inheritdoc />
    public override string ToString() => NodeAddress;

    internal void CheckValid(ISession? session)
    {
        if (session == null) return;

        try
        {
            string address = NodeAddress.Trim();
            if (address.StartsWith("nsu=", StringComparison.Ordinal))
            {
                ExpandedNodeId = global::Opc.Ua.ExpandedNodeId.Parse(address);
                NodeId = global::Opc.Ua.ExpandedNodeId.ToNodeId(ExpandedNodeId!, session.NamespaceUris);
            }
            else if (address.StartsWith("ns=", StringComparison.Ordinal)
                     || address.StartsWith("i=", StringComparison.Ordinal))
            {
                NodeId = global::Opc.Ua.NodeId.Parse(address);
                ExpandedNodeId = global::Opc.Ua.NodeId.ToExpandedNodeId(NodeId!, session.NamespaceUris);
            }
        }
        catch
        {
            NodeId = null;
            ExpandedNodeId = null;
        }
    }

    private bool CheckIsValid()
    {
        if (NodeId == null || ExpandedNodeId == null)
            return false;

        if (ExpandedNodeId.NamespaceIndex > 0 && string.IsNullOrEmpty(ExpandedNodeId.NamespaceUri))
            return false;

        return true;
    }
}
