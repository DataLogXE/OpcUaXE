using OpcUaXE.Client.Types;

namespace OpcUaXE.UnitTests.Types;

public sealed class XeWriteItemTests
{
    [Fact]
    public void Constructor_SetsAddressAndValue()
    {
        var item = new XeWriteItem("ns=2;s=BoolNode", true);

        item.Address.NodeAddress.Should().Be("ns=2;s=BoolNode");
        item.Value.Should().Be(true);
    }

    [Fact]
    public void Constructor_WithNullValue_IsAllowed()
    {
        var item = new XeWriteItem("ns=2;s=StringNode", null);

        item.Value.Should().BeNull();
    }

    [Fact]
    public void DefaultState_IsGood()
    {
        var item = new XeWriteItem("ns=2;s=Int32Node", 42);

        item.State.IsGood.Should().BeTrue();
        item.State.IsBad.Should().BeFalse();
        item.State.IsUncertain.Should().BeFalse();
    }

    [Theory]
    [InlineData("ns=2;s=BoolNode", true)]
    [InlineData("ns=2;s=Int32Node", 99)]
    [InlineData("ns=2;s=StringNode", "hello")]
    [InlineData("ns=2;s=DoubleNode", 1.23d)]
    public void ToString_ContainsAddressAndValue(string address, object value)
    {
        var item = new XeWriteItem(address, value);

        string text = item.ToString();

        text.Should().Contain(address);
        text.Should().Contain(value.ToString());
    }
}
