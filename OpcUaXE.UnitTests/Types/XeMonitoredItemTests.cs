using OpcUaXE.Client.Types;

namespace OpcUaXE.UnitTests.Types;

public sealed class XeMonitoredItemTests
{
    [Fact]
    public void Constructor_DefaultIntervals_AreZeroAndThousand()
    {
        var item = new XeMonitoredItem("ns=2;s=BoolNode");

        item.Address.NodeAddress.Should().Be("ns=2;s=BoolNode");
        item.SamplingIntervalMs.Should().Be(0);
        item.PublishIntervalMs.Should().Be(1000);
    }

    [Fact]
    public void Constructor_CustomIntervals_AreStored()
    {
        var item = new XeMonitoredItem("ns=2;s=Int32Node", samplingIntervalMs: 250, publishIntervalMs: 500);

        item.SamplingIntervalMs.Should().Be(250);
        item.PublishIntervalMs.Should().Be(500);
    }

    [Fact]
    public void ToString_ContainsAddress()
    {
        var item = new XeMonitoredItem("ns=2;s=MonitorNode");

        item.ToString().Should().Contain("ns=2;s=MonitorNode");
    }

    [Fact]
    public void AddressIsNotValid_WithoutSession()
    {
        var item = new XeMonitoredItem("ns=2;s=BoolNode");

        // Without a live session the address cannot be resolved.
        item.Address.IsValid.Should().BeFalse();
    }
}
