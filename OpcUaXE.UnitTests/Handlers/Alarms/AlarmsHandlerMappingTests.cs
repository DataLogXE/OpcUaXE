using Opc.Ua;
using OpcUaXE.Client.Core;
using OpcUaXE.Client.Core.Handlers;
using OpcUaXE.Client.Events;

namespace OpcUaXE.UnitTests.Handlers.Alarms;

/// <summary>
/// Unit tests for A&amp;C event filter shape and <see cref="AlarmsHandler.MapFromEventFields"/> mapping
/// (no live OPC UA session required).
/// </summary>
public sealed class AlarmsHandlerMappingTests
{
    [Fact]
    public void BuildEventFilter_HasExpectedSelectClauseCount()
    {
        EventFilter filter = AlarmsHandler.BuildEventFilter();

        filter.SelectClauses.Should().NotBeNull();
        filter.SelectClauses.Count.Should().Be(11, "indices [0]–[10] per MapFromEventFields");
    }

    [Fact]
    public void MapFromEventFields_EmptyCollection_ReturnsDefaults()
    {
        var handler = new AlarmsHandler(new XeClientContext());
        var fieldList = new EventFieldList { EventFields = [] };

        XeAlarmAndConditionEventArgs mapped = handler.MapFromEventFields(fieldList);

        mapped.EventId.Should().BeNull();
        mapped.EventTypeNodeId.Should().BeNull();
        mapped.SourceName.Should().BeNull();
        mapped.Severity.Should().Be(0);
        mapped.ConditionName.Should().BeNull();
        mapped.Retain.Should().BeNull();
        mapped.ActiveState.Should().BeNull();
        mapped.AckedState.Should().BeNull();
        mapped.Message.Should().BeNull();
    }

    [Fact]
    public void MapFromEventFields_EncodesEventIdAsHex()
    {
        var handler = new AlarmsHandler(new XeClientContext());
        var fields = new VariantCollection { new Variant(new byte[] { 0x01, 0xFF }) };
        var fieldList = new EventFieldList { EventFields = fields };

        XeAlarmAndConditionEventArgs mapped = handler.MapFromEventFields(fieldList);

        mapped.EventId.Should().Be("01FF");
    }

    [Fact]
    public void MapFromEventFields_MapsStandardFields()
    {
        var handler = new AlarmsHandler(new XeClientContext());
        var time = new DateTime(2025, 3, 15, 8, 0, 0, DateTimeKind.Utc);
        var receive = new DateTime(2025, 3, 15, 8, 0, 1, DateTimeKind.Utc);
        NodeId typeId = ObjectTypeIds.AlarmConditionType;

        var fields = new VariantCollection
        {
            new Variant(new byte[] { 1 }),
            new Variant(typeId),
            new Variant("SourceA"),
            new Variant(time),
            new Variant(receive),
            new Variant(new LocalizedText("en", "Alarm text")),
            new Variant((ushort)750),
            new Variant("CondX"),
            new Variant(true),
            new Variant(true),
            new Variant(false)
        };
        var fieldList = new EventFieldList { EventFields = fields };

        XeAlarmAndConditionEventArgs mapped = handler.MapFromEventFields(fieldList);

        mapped.EventId.Should().Be("01");
        mapped.EventTypeNodeId.Should().Be(typeId.ToString());
        mapped.SourceName.Should().Be("SourceA");
        mapped.TimeUtc.Should().Be(time);
        mapped.ReceiveTimeUtc.Should().Be(receive);
        mapped.Message.Should().Be("Alarm text");
        mapped.Severity.Should().Be(750);
        mapped.ConditionName.Should().Be("CondX");
        mapped.Retain.Should().BeTrue();
        mapped.ActiveState.Should().BeTrue();
        mapped.AckedState.Should().BeFalse();
    }

    [Fact]
    public void MapFromEventFields_MessageAcceptsPlainString()
    {
        var handler = new AlarmsHandler(new XeClientContext());
        var fields = new VariantCollection
        {
            default,
            default,
            default,
            default,
            default,
            new Variant("plain"),
            default,
            default,
            default,
            default,
            default
        };
        var fieldList = new EventFieldList { EventFields = fields };

        handler.MapFromEventFields(fieldList).Message.Should().Be("plain");
    }

    [Fact]
    public void MapFromEventFields_Severity_ClampedFromInt()
    {
        var handler = new AlarmsHandler(new XeClientContext());
        var fields = new VariantCollection
        {
            default, default, default, default, default, default,
            new Variant(65536),
            default, default, default, default
        };
        var fieldList = new EventFieldList { EventFields = fields };

        handler.MapFromEventFields(fieldList).Severity.Should().Be(ushort.MaxValue);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData("Inactive", false)]
    [InlineData("ACTIVE", true)]
    [InlineData("0", false)]
    [InlineData("1", true)]
    public void MapFromEventFields_BooleanFields_ParseLocalizedTextId(string text, bool expected)
    {
        var handler = new AlarmsHandler(new XeClientContext());
        var fields = new VariantCollection
        {
            default, default, default, default, default, default, default, default, default,
            new Variant(new LocalizedText("en", text)),
            new Variant(new LocalizedText("en", text))
        };
        var fieldList = new EventFieldList { EventFields = fields };

        XeAlarmAndConditionEventArgs mapped = handler.MapFromEventFields(fieldList);

        mapped.ActiveState.Should().Be(expected);
        mapped.AckedState.Should().Be(expected);
    }
}
