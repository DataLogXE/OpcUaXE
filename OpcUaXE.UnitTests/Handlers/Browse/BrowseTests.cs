using OpcUaXE.Client.Exceptions;
using OpcUaXE.UnitTests.Infrastructure;

namespace OpcUaXE.UnitTests.Handlers.Browse;

[Collection("OpcUaServer")]
public sealed class BrowseTests
{
    // ROOT FOLDER (BrowseRootAsync) ##########

    [Fact]
    public async Task BrowseRootAsync_ReturnsNonEmptyList()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        var results = await client.BrowseRootAsync();

        results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task BrowseRootAsync_ContainsObjectsFolder()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        var results = await client.BrowseRootAsync();

        results.Should().Contain(r =>
            r.DisplayName.Equals("Objects", StringComparison.OrdinalIgnoreCase),
            "the standard OPC UA Objects folder must be a direct child of the root node");
    }

    [Fact]
    public async Task BrowseRootAsync_ResultItems_HaveNonEmptyNodeIdAndDisplayName()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        var results = await client.BrowseRootAsync();

        results.Should().OnlyContain(r => !string.IsNullOrEmpty(r.NodeId));
        results.Should().OnlyContain(r => !string.IsNullOrEmpty(r.DisplayName));
    }

    [Fact]
    public async Task BrowseRootAsync_WhenNotConnected_ThrowsXeBrowseException()
    {
        await using var client = new OpcUaXE.Client.XeClient();

        Func<Task> act = () => client.BrowseRootAsync();

        await act.Should().ThrowAsync<XeBrowseException>();
    }

    // ROOT FOLDER (BrowseAsync) ##########

    [Fact]
    public async Task BrowseAsync_RootFolder_ReturnsNonEmptyList()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        // i=84 = RootFolder in the OPC UA standard namespace.
        var results = await client.BrowseAsync("i=84");

        results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task BrowseAsync_ObjectsFolder_ContainsTestNodesFolder()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        // i=85 = ObjectsFolder
        var results = await client.BrowseAsync("i=85");

        results.Should().Contain(r =>
            r.DisplayName.Equals("TestNodes", StringComparison.OrdinalIgnoreCase),
            "the TestNodes folder registered by TestNodeManager must appear");
    }

    // CUSTOM TEST NODES ##########

    [Fact]
    public async Task BrowseAsync_TestNodesFolder_ContainsAllTestVariables()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        var results = (await client.BrowseAsync("ns=2;s=TestNodes")).ToList();

        results.Should().NotBeEmpty();

        string[] expectedNames =
        [
            "BoolNode", "Int32Node", "DoubleNode", "StringNode", "MonitorNode"
        ];

        foreach (string name in expectedNames)
        {
            results.Should().Contain(r =>
                r.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase),
                $"node '{name}' must be found under TestNodes");
        }
    }

    // BROWSERESULTITEM PROPERTIES ##########

    [Fact]
    public async Task BrowseAsync_ResultItems_HaveNonEmptyNodeId()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        var results = await client.BrowseAsync("i=85");

        results.Should().OnlyContain(r => !string.IsNullOrEmpty(r.NodeId));
    }

    [Fact]
    public async Task BrowseAsync_ResultItems_HaveNonEmptyDisplayName()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        var results = await client.BrowseAsync("i=85");

        results.Should().OnlyContain(r => !string.IsNullOrEmpty(r.DisplayName));
    }

    // ERROR CASES ##########

    [Fact]
    public async Task BrowseAsync_WhenNotConnected_ThrowsXeBrowseException()
    {
        await using var client = new OpcUaXE.Client.XeClient();

        Func<Task> act = () => client.BrowseAsync("i=84");

        await act.Should().ThrowAsync<XeBrowseException>();
    }

    [Fact]
    public async Task BrowseAsync_InvalidAddress_ThrowsXeBrowseException()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        Func<Task> act = () => client.BrowseAsync("not_a_valid_node_id_@@");

        await act.Should().ThrowAsync<XeBrowseException>();
    }
}
