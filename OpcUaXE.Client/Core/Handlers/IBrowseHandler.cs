using OpcUaXE.Client.Types;

namespace OpcUaXE.Client.Core.Handlers;

/// <summary>Browses the OPC UA address space.</summary>
internal interface IBrowseHandler
{
    /// <summary>Browses the child nodes of the node identified by the given address string.</summary>
    Task<IReadOnlyList<XeBrowseResultItem>> BrowseAsync(string nodeAddress, CancellationToken ct);

    /// <summary>Browses the direct children of the OPC UA root node (<c>i=84</c>).</summary>
    Task<IReadOnlyList<XeBrowseResultItem>> BrowseRootAsync(CancellationToken ct);
}
