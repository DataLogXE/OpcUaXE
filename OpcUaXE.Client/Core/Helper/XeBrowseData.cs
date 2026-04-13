using Opc.Ua;
using OpcUaXE.Client.Types;

namespace OpcUaXE.Client.Core.Helper;

internal class XeBrowseData
{
    /// <summary>Browse results from the initial browse call.</summary>
    internal BrowseResultCollection? BrowseResults;

    /// <summary>All collected reference descriptions across all browse pages.</summary>
    internal List<ReferenceDescription> AllReferences = [];

    /// <summary>Continuation points for paged browse operations.</summary>
    internal ByteStringCollection ContinuationPoints = [];

    /// <summary>Maps <see cref="AllReferences"/> to <see cref="XeBrowseResultItem"/> objects.</summary>
    internal IReadOnlyList<XeBrowseResultItem> GetBrowseResult()
    {
        List<XeBrowseResultItem> browseResults = [];
        AllReferences.ForEach(r => { browseResults.Add(new XeBrowseResultItem(r)); });
        return browseResults;
    }
}
