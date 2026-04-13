using Opc.Ua;
using Opc.Ua.Client;
using OpcUaXE.Client.Core;
using OpcUaXE.Client.Core.Handlers;
using OpcUaXE.Client.Events;

namespace OpcUaXE.UnitTests.Handlers.Alarms;

public sealed class AlarmsHandlerNotificationTests
{
    [Fact]
    public async Task NotificationLoopAsync_RaisesAlarmEvent_WhenTestNotificationIsEnqueued()
    {
        using var ctx = new XeClientContext();
        var handler = new AlarmsHandler(ctx);
        XeAlarmAndConditionEventArgs? received = null;
        handler.AlarmAndConditionEventReceived += (_, e) => received = e;

        Task loop = Task.Run(() => handler.NotificationLoopAsync());
        await Task.Delay(50);

        var fields = new VariantCollection
        {
            default,
            default,
            default,
            default,
            default,
            new Variant("pipeline-test"),
            default,
            default,
            default,
            default,
            default
        };
        var fieldList = new EventFieldList { EventFields = fields };

        handler.TryEnqueueTestNotification(fieldList).Should().BeTrue();

        await WaitUntilAsync(() => received != null, TimeSpan.FromSeconds(2));

        received.Should().NotBeNull();
        received!.Message.Should().Be("pipeline-test");

        ctx.Cancel();
        await Task.WhenAny(loop, Task.Delay(3000));
    }

    [Fact]
    public void XeClientContext_AcSubscriptionName_MatchesAlarmsHandlerSubscription()
    {
        XeClientContext.AcSubscriptionName.Should().Be("OpcUaXE_AlarmsAndConditions");
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline && !condition())
            await Task.Delay(15);
    }
}
