using Opc.Ua;
using OpcUaXE.Client.Core.Helper;
using OpcUaXE.Client.Exceptions;
using OpcUaXE.Client.Types;

namespace OpcUaXE.Client.Core.Handlers;

/// <summary>
/// Provides OPC UA address-space browsing, returning hierarchical child nodes
/// of a given start node.
/// </summary>
internal sealed class BrowseHandler : IBrowseHandler
{
    private readonly XeClientContext _ctx;

    public BrowseHandler(XeClientContext ctx) => _ctx = ctx;

    #region Public API

    public Task<IReadOnlyList<XeBrowseResultItem>> BrowseRootAsync(CancellationToken ct) =>
        BrowseAsync(ObjectIds.RootFolder.ToString(), ct);

    public async Task<IReadOnlyList<XeBrowseResultItem>> BrowseAsync(
        string nodeAddress, CancellationToken ct)
    {
        if (!_ctx.IsConnected)
            throw new XeBrowseException("Client is not connected to the server.");

        XeNodeAddress address = new(nodeAddress, _ctx.Session);
        if (!address.IsValid)
            throw new XeBrowseException(
                $"Invalid or unresolvable node address: '{nodeAddress}'");

        try
        {
            XeBrowseData data = new();
            await BrowseInternalAsync(address, data, ct).ConfigureAwait(false);
            await BrowseNextInternalAsync(data, ct).ConfigureAwait(false);
            await BrowseReleaseContinuationPointsAsync(data, ct).ConfigureAwait(false);
            return data.GetBrowseResult();
        }
        catch (XeBrowseException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new XeBrowseException("Browse operation failed.", ex);
        }
    }

    #endregion

    #region Private Implementation

    private static BrowseDescriptionCollection GetBrowseDescriptions(string startNodeId) =>
    [
        new BrowseDescription
        {
            NodeId = startNodeId,
            BrowseDirection = BrowseDirection.Forward,
            ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
            IncludeSubtypes = true,
            NodeClassMask = (uint)NodeClass.Unspecified,
            ResultMask = (uint)BrowseResultMask.All
        }
    ];

    private async Task BrowseInternalAsync(
        XeNodeAddress address, XeBrowseData data, CancellationToken ct)
    {
        BrowseDescriptionCollection nodesToBrowse =
            GetBrowseDescriptions(address.NodeIdString);

        BrowseResponse response = await _ctx.Session!.BrowseAsync(
            requestHeader: null,
            view: null,
            requestedMaxReferencesPerNode: 0,
            nodesToBrowse: nodesToBrowse,
            ct).ConfigureAwait(false);

        BrowseResultCollection? results = response.Results;
        if (results?.Count > 0)
        {
            if (results[0].References != null)
                data.AllReferences.AddRange(results[0].References);

            if (results[0].ContinuationPoint?.Length > 0)
                data.ContinuationPoints.Add(results[0].ContinuationPoint);
        }

        data.BrowseResults = results;
    }

    private async Task BrowseNextInternalAsync(XeBrowseData data, CancellationToken ct)
    {
        ByteStringCollection continuationPoints = new(data.ContinuationPoints);
        BrowseResultCollection? results = data.BrowseResults;

        while (results?.Count > 0 && results[0].ContinuationPoint != null)
        {
            BrowseNextResponse nextResponse = await _ctx.Session!.BrowseNextAsync(
                requestHeader: null,
                releaseContinuationPoints: false,
                continuationPoints: continuationPoints,
                ct).ConfigureAwait(false);

            results = nextResponse.Results;
            if (results?.Count > 0)
            {
                if (results[0].References != null)
                    data.AllReferences.AddRange(results[0].References);

                if (results[0].ContinuationPoint?.Length > 0)
                    data.ContinuationPoints.Add(results[0].ContinuationPoint);
            }
        }
    }

    private async Task BrowseReleaseContinuationPointsAsync(
        XeBrowseData data, CancellationToken ct)
    {
        if (data.ContinuationPoints.Count == 0) return;

        try
        {
            await _ctx.Session!.BrowseNextAsync(
                requestHeader: null,
                releaseContinuationPoints: true,
                continuationPoints: data.ContinuationPoints,
                ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Releasing continuation points is best-effort.
            _ctx.RaiseServiceException(ex);
        }
    }

    #endregion
}
