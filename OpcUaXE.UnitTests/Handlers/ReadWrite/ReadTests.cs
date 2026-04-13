using OpcUaXE.Client.Exceptions;
using OpcUaXE.UnitTests.Infrastructure;

namespace OpcUaXE.UnitTests.Handlers.ReadWrite;

[Collection("OpcUaServer")]
public sealed class ReadTests
{
    // SINGLE NODE READS ##########

    [Fact]
    public async Task ReadValueAsync_BoolNode_ReturnsGoodStatus()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        var result = await client.ReadValueAsync(TestNodeIds.BoolNode);

        result.State.IsGood.Should().BeTrue();
    }

    [Fact]
    public async Task ReadValueAsync_Int32Node_ReturnsInitialValue42()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        var result = await client.ReadValueAsync(TestNodeIds.Int32Node);

        result.State.IsGood.Should().BeTrue();
        ((int)result.Value).Should().Be(42);
    }

    [Fact]
    public async Task ReadValueAsync_DoubleNode_ReturnsNumericValue()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        var result = await client.ReadValueAsync(TestNodeIds.DoubleNode);

        result.State.IsGood.Should().BeTrue();
        ((double)result.Value).Should().BeApproximately(3.14d, 0.001d);
    }

    [Fact]
    public async Task ReadValueAsync_StringNode_ReturnsHello()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        var result = await client.ReadValueAsync(TestNodeIds.StringNode);

        result.State.IsGood.Should().BeTrue();
        ((string)result.Value).Should().Be("hello");
    }

    [Fact]
    public async Task ReadValueAsync_Address_IsPreserved()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        var result = await client.ReadValueAsync(TestNodeIds.BoolNode);

        result.Address.NodeAddress.Should().Be(TestNodeIds.BoolNode);
    }

    [Fact]
    public async Task ReadValueAsync_ServerTimeUtc_IsReasonable()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        var result = await client.ReadValueAsync(TestNodeIds.Int32Node);

        // Tolerance covers full test-suite runtime; timestamp is set when the in-process
        // server creates the node and is not refreshed until the value is written.
        result.ServerTimeUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
    }

    // BATCH READS ##########

    [Fact]
    public async Task ReadValuesAsync_MultiplNodes_ReturnsCorrectCount()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        var results = await client.ReadValuesAsync(
        [
            TestNodeIds.BoolNode,
            TestNodeIds.Int32Node,
            TestNodeIds.DoubleNode,
            TestNodeIds.StringNode
        ]);

        results.Should().HaveCount(4);
        results.Should().OnlyContain(r => r.State.IsGood);
    }

    [Fact]
    public async Task ReadValuesAsync_OrderMatches_InputOrder()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();
        string[] addresses =
        [
            TestNodeIds.StringNode,
            TestNodeIds.Int32Node,
            TestNodeIds.BoolNode
        ];

        var results = await client.ReadValuesAsync(addresses);

        for (int i = 0; i < addresses.Length; i++)
            results[i].Address.NodeAddress.Should().Be(addresses[i]);
    }

    [Fact]
    public async Task ReadValuesAsync_EmptyList_ReturnsEmptyResult()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        var results = await client.ReadValuesAsync([]);

        results.Should().BeEmpty();
    }

    // ERROR CASES ##########

    [Fact]
    public async Task ReadValueAsync_WhenNotConnected_ThrowsXeConnectionException()
    {
        await using var client = new OpcUaXE.Client.XeClient();

        Func<Task> act = () => client.ReadValueAsync(TestNodeIds.BoolNode);

        await act.Should().ThrowAsync<XeConnectionException>();
    }
}
