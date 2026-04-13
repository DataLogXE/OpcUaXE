using OpcUaXE.Client.Exceptions;
using OpcUaXE.Client.Types;
using OpcUaXE.UnitTests.Infrastructure;

namespace OpcUaXE.UnitTests.Handlers.ReadWrite;

[Collection("OpcUaServer")]
public sealed class WriteTests
{
    // ROUNDTRIP: WRITE THEN READ BACK ##########

    [Fact]
    public async Task WriteAndRead_BoolNode_Roundtrip()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        await client.WriteValueAsync(TestNodeIds.BoolNode, true);
        var result = await client.ReadValueAsync(TestNodeIds.BoolNode);

        result.State.IsGood.Should().BeTrue();
        ((bool)result.Value).Should().BeTrue();
    }

    [Fact]
    public async Task WriteAndRead_Int32Node_Roundtrip()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();
        const int expected = 99;

        await client.WriteValueAsync(TestNodeIds.Int32Node, expected);
        var result = await client.ReadValueAsync(TestNodeIds.Int32Node);

        result.State.IsGood.Should().BeTrue();
        ((int)result.Value).Should().Be(expected);
    }

    [Fact]
    public async Task WriteAndRead_DoubleNode_Roundtrip()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();
        const double expected = 2.718d;

        await client.WriteValueAsync(TestNodeIds.DoubleNode, expected);
        var result = await client.ReadValueAsync(TestNodeIds.DoubleNode);

        result.State.IsGood.Should().BeTrue();
        ((double)result.Value).Should().BeApproximately(expected, 0.0001d);
    }

    [Fact]
    public async Task WriteAndRead_StringNode_Roundtrip()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();
        const string expected = "test_value";

        await client.WriteValueAsync(TestNodeIds.StringNode, expected);
        var result = await client.ReadValueAsync(TestNodeIds.StringNode);

        result.State.IsGood.Should().BeTrue();
        ((string)result.Value).Should().Be(expected);
    }

    // XEWRITEITEM OVERLOAD ##########

    [Fact]
    public async Task WriteValueAsync_XeWriteItem_StateIsGoodAfterWrite()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();
        var item = new XeWriteItem(TestNodeIds.Int32Node, 77);

        await client.WriteValueAsync(item);

        item.State.IsGood.Should().BeTrue();
    }

    [Fact]
    public async Task WriteValueAsync_ReturnsXeWriteItem_WithGoodStatus()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        var item = await client.WriteValueAsync(TestNodeIds.Int32Node, 55);

        item.State.IsGood.Should().BeTrue();
        item.Address.NodeAddress.Should().Be(TestNodeIds.Int32Node);
    }

    // BATCH WRITE ##########

    [Fact]
    public async Task WriteValuesAsync_MultiplItems_AllSucceed()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();
        var items = new List<XeWriteItem>
        {
            new(TestNodeIds.Int32Node, 11),
            new(TestNodeIds.DoubleNode, 1.1d),
            new(TestNodeIds.StringNode, "batch")
        };

        await client.WriteValuesAsync(items);

        items.Should().OnlyContain(i => i.State.IsGood);
    }

    // TYPE COERCION ##########

    [Fact]
    public async Task WriteValueAsync_IntToDoubleNode_TypeCoercionSucceeds()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        // Write an int to a Double-typed node – should be coerced automatically.
        var item = await client.WriteValueAsync(TestNodeIds.DoubleNode, (int)7);

        item.State.IsGood.Should().BeTrue();

        var readBack = await client.ReadValueAsync(TestNodeIds.DoubleNode);
        ((double)readBack.Value).Should().BeApproximately(7.0d, 0.0001d);
    }

    // ERROR CASES ##########

    [Fact]
    public async Task WriteValueAsync_WhenNotConnected_ThrowsXeConnectionException()
    {
        await using var client = new OpcUaXE.Client.XeClient();

        Func<Task> act = () => client.WriteValueAsync(TestNodeIds.Int32Node, 1);

        await act.Should().ThrowAsync<XeConnectionException>();
    }
}
