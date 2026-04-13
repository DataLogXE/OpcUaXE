using OpcUaXE.Client.Events;

namespace OpcUaXE.UnitTests.Handlers.Alarms;

public sealed class XeAlarmAndConditionEventArgsTests
{
    [Fact]
    public void ToString_IncludesCoreFields_WhenPopulated()
    {
        var t = new DateTime(2024, 6, 1, 12, 30, 45, 123, DateTimeKind.Utc);
        var args = new XeAlarmAndConditionEventArgs
        {
            EventTypeNodeId = "i=12345",
            SourceName = "PLC1",
            TimeUtc = t,
            ReceiveTimeUtc = t,
            ConditionName = "TankHigh",
            Retain = true,
            ActiveState = true,
            AckedState = false,
            Message = "Level exceeded",
            Severity = 500
        };

        string s = args.ToString();

        s.Should().Contain("2024-06-01T12:30:45.123000Z");
        s.Should().Contain("EventType=i=12345");
        s.Should().Contain("ConditionName=TankHigh");
        s.Should().Contain("Retain=True");
        s.Should().Contain("ActiveState=True");
        s.Should().Contain("AckedState=False");
        s.Should().Contain("Severity=500");
        s.Should().Contain("Message=Level exceeded");
    }

    [Fact]
    public void ToString_UsesEventTypeNodeId_WhenDisplayNameUnset()
    {
        var args = new XeAlarmAndConditionEventArgs
        {
            EventTypeNodeId = "ns=2;s=MyEventType",
            TimeUtc = default
        };

        args.ToString().Should().Contain("EventType=ns=2;s=MyEventType");
    }

    [Fact]
    public void AllProperties_DefaultToNullOrDefault_WhenOmitted()
    {
        var args = new XeAlarmAndConditionEventArgs();

        args.EventId.Should().BeNull();
        args.EventTypeNodeId.Should().BeNull();
        args.SourceName.Should().BeNull();
        args.TimeUtc.Should().Be(default);
        args.ReceiveTimeUtc.Should().Be(default);
        args.ConditionName.Should().BeNull();
        args.ActiveState.Should().BeNull();
        args.AckedState.Should().BeNull();
        args.Retain.Should().BeNull();
        args.Message.Should().BeNull();
        args.Severity.Should().Be(0);
    }

    [Fact]
    public void EventId_CanBeSet()
    {
        var args = new XeAlarmAndConditionEventArgs { EventId = "AABBCC" };

        args.EventId.Should().Be("AABBCC");
    }
}
