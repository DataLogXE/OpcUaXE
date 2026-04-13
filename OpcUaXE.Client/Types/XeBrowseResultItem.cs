using Opc.Ua;

namespace OpcUaXE.Client.Types;

/// <summary>A single result item returned by a browse operation.</summary>
public sealed class XeBrowseResultItem
{
    /// <summary>
    /// Gets the node identifier as a string (e.g. <c>"ns=2;s=MyNode"</c> or <c>"i=84"</c>).
    /// Pass directly to <see cref="OpcUaXE.Client.XeClient"/>.<c>BrowseAsync</c> to browse this node's children.
    /// </summary>
    public string NodeId { get; }

    /// <summary>Gets the display name of the referenced node.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the node class as a descriptive string (e.g. <c>"Object"</c>, <c>"Variable"</c>).</summary>
    public string NodeClass { get; }

    internal XeBrowseResultItem(ReferenceDescription reference)
    {
        NodeId = reference.NodeId.ToString();
        DisplayName = reference.DisplayName?.Text ?? string.Empty;
        NodeClass = reference.NodeClass.ToString();
    }

    /// <inheritdoc />
    public override string ToString() => $"NodeId={NodeId}; DisplayName={DisplayName}; NodeClass={NodeClass}; ";
}
