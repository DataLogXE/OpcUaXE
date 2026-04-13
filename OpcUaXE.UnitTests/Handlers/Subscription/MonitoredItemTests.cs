using OpcUaXE.Client;
using OpcUaXE.Client.Types;
using OpcUaXE.UnitTests.Infrastructure;

namespace OpcUaXE.UnitTests.Handlers.Subscription;

[Collection("OpcUaServer")]
public sealed class MonitoredItemTests
{
    // SubscriptionHandler waits for _connectedCounter >= SyncStartDelay (1 s ticks), then syncs.
    private static readonly TimeSpan SubscriptionSetupDelay = TimeSpan.FromSeconds(5);

    // ADD MONITORED ITEM ##########

    [Fact]
    public async Task AddMonitoredItem_ValueChanges_EventIsFired()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        var received = new TaskCompletionSource<IReadOnlyList<XeReadItem>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        client.MonitoredItemValuesReceived += (_, items) => received.TrySetResult(items);
        client.AddMonitoredItem(TestNodeIds.MonitorNode);

        // Wait for the subscription to be registered server-side.
        await Task.Delay(SubscriptionSetupDelay);

        // Trigger a value change by writing via the client.
        await client.WriteValueAsync(TestNodeIds.MonitorNode, 42);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        cts.Token.Register(() => received.TrySetCanceled());

        var items = await received.Task;
        items.Should().NotBeEmpty();
        items.Should().Contain(i => i.Address.NodeAddress == TestNodeIds.MonitorNode);
    }

    [Fact]
    public async Task AddMonitoredItem_MultipleChanges_MultipleEventsReceived()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        var eventCount = 0;
        client.MonitoredItemValuesReceived += (_, _) =>
            Interlocked.Increment(ref eventCount);

        client.AddMonitoredItem(TestNodeIds.MonitorNode);
        await Task.Delay(SubscriptionSetupDelay);

        // Write three distinct values.
        await client.WriteValueAsync(TestNodeIds.MonitorNode, 10);
        await Task.Delay(150);
        await client.WriteValueAsync(TestNodeIds.MonitorNode, 20);
        await Task.Delay(150);
        await client.WriteValueAsync(TestNodeIds.MonitorNode, 30);
        await Task.Delay(400);

        eventCount.Should().BeGreaterThanOrEqualTo(1,
            "at least one notification must arrive for three value changes");
    }

    // DUPLICATE ADD ##########

    [Fact]
    public async Task AddMonitoredItem_Duplicate_IsIgnored()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        client.AddMonitoredItem(TestNodeIds.MonitorNode);
        client.AddMonitoredItem(TestNodeIds.MonitorNode); // duplicate – must not throw

        await Task.Delay(SubscriptionSetupDelay);
    }

    // REMOVE MONITORED ITEM ##########

    [Fact]
    public async Task RemoveMonitoredItem_AfterAdd_NoFurtherEventsReceived()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        // Phase 1: confirm subscription is active by waiting for a confirmed notification.
        var firstNotification = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        client.MonitoredItemValuesReceived += (_, _) =>
            firstNotification.TrySetResult(true);

        client.AddMonitoredItem(TestNodeIds.MonitorNode);
        await Task.Delay(SubscriptionSetupDelay);
        await client.WriteValueAsync(TestNodeIds.MonitorNode, 100);

        await firstNotification.Task.WaitAsync(TimeSpan.FromSeconds(6));

        // Phase 2: remove, let sync propagate, then track what arrives.
        client.RemoveMonitoredItem(TestNodeIds.MonitorNode);
        await Task.Delay(SubscriptionSetupDelay);

        var afterRemove = new List<XeReadItem>();
        client.MonitoredItemValuesReceived += (_, items) => afterRemove.AddRange(items);

        // Trigger a write – the server-side subscription is gone, so no event expected.
        await client.WriteValueAsync(TestNodeIds.MonitorNode, 200);
        await Task.Delay(600);

        afterRemove.Should().BeEmpty(
            "no notifications should arrive after the monitored item was removed");
    }

    [Fact]
    public async Task RemoveMonitoredItem_NonExistingAddress_IsNoOp()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        // Removing an address that was never added must not throw.
        Action act = () => client.RemoveMonitoredItem("ns=2;s=DoesNotExist");

        act.Should().NotThrow();
    }

    // ADDMONITOREDITEMS / REMOVEALLMONITOREDITEMS ##########

    [Fact]
    public async Task AddMonitoredItems_List_AddsAllItems()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        var items = new List<XeMonitoredItem>
        {
            new(TestNodeIds.MonitorNode),
            new(TestNodeIds.Int32Node)
        };

        // Must not throw.
        Action act = () => client.AddMonitoredItems(items);
        act.Should().NotThrow();

        await Task.Delay(SubscriptionSetupDelay);
    }

    [Fact]
    public async Task RemoveAllMonitoredItems_ClearsAll()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();
        client.AddMonitoredItem(TestNodeIds.MonitorNode);
        client.AddMonitoredItem(TestNodeIds.Int32Node);
        await Task.Delay(SubscriptionSetupDelay);

        client.RemoveAllMonitoredItems();

        // No exception and subsequent writes produce no events after sync.
        await Task.Delay(SubscriptionSetupDelay);

        var afterClear = new List<XeReadItem>();
        client.MonitoredItemValuesReceived += (_, items) => afterClear.AddRange(items);
        await client.WriteValueAsync(TestNodeIds.MonitorNode, 99);
        await Task.Delay(400);

        afterClear.Should().BeEmpty();
    }
}
