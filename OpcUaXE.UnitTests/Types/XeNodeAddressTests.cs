using OpcUaXE.Client.Types;

namespace OpcUaXE.UnitTests.Types;

public sealed class XeNodeAddressTests
{
    [Theory]
    [InlineData("ns=2;s=BoolNode")]
    [InlineData("ns=1;i=1001")]
    [InlineData("i=84")]
    public void Constructor_StoresOriginalAddress(string address)
    {
        var nodeAddress = new XeNodeAddress(address);

        nodeAddress.NodeAddress.Should().Be(address);
    }

    [Fact]
    public void IsValid_IsFalse_WithoutSession()
    {
        // No session supplied → cannot resolve namespace table.
        var nodeAddress = new XeNodeAddress("ns=2;s=BoolNode");

        nodeAddress.IsValid.Should().BeFalse();
    }

    [Fact]
    public void NodeIdString_IsEmpty_WhenNotValid()
    {
        var nodeAddress = new XeNodeAddress("ns=2;s=BoolNode");

        nodeAddress.NodeIdString.Should().BeEmpty();
    }

    [Fact]
    public void ToString_ReturnsOriginalAddress()
    {
        const string addr = "ns=2;s=MyNode";
        var nodeAddress = new XeNodeAddress(addr);

        nodeAddress.ToString().Should().Be(addr);
    }
}
