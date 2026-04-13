using System.Collections.Generic;
using System.Diagnostics;
using OpcUaXE.Client;
using OpcUaXE.Client.Events;
using OpcUaXE.UnitTests.Infrastructure;

namespace OpcUaXE.UnitTests.Handlers.Alarms;

/// <summary>
/// End-to-end Alarms and Conditions against the in-process <see cref="TestOpcUaServer"/>.
/// Scenarios are split so each test stays within a 30 s wall-clock budget (connect + waits).
/// </summary>
[Collection("OpcUaServer")]
public sealed class AlarmIntegrationTests
{
    private readonly OpcUaServerFixture _fixture;

    public AlarmIntegrationTests(OpcUaServerFixture fixture) => _fixture = fixture;

    /// <summary>Allows <see cref="OpcUaXE.Client.Core.Handlers.AlarmsHandler"/> to reach Running.</summary>
    private static readonly TimeSpan AcReadyDelay = TimeSpan.FromSeconds(6);

    private static bool IsOurIntegrationTestAlarm(XeAlarmAndConditionEventArgs e)
    {
        if (e.ConditionName == TestNodeManager.IntegrationAlarmConditionName)
            return true;

        return e.Message != null
            && e.Message.Contains("integration alarm", StringComparison.OrdinalIgnoreCase);
    }

    private static XeAlarmAndConditionEventArgs? LastOurAlarmSnapshot(IReadOnlyList<XeAlarmAndConditionEventArgs> all)
    {
        for (int i = all.Count - 1; i >= 0; i--)
        {
            if (IsOurIntegrationTestAlarm(all[i]))
                return all[i];
        }

        return null;
    }

    private static async Task<XeAlarmAndConditionEventArgs> WaitForIntegrationAsync(
        List<XeAlarmAndConditionEventArgs> received,
        object receivedLock,
        Func<XeAlarmAndConditionEventArgs, bool> match,
        TimeSpan timeout,
        string because)
    {
        Stopwatch sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            XeAlarmAndConditionEventArgs? last;
            lock (receivedLock)
                last = LastOurAlarmSnapshot(received);

            if (last != null && match(last))
                return last;

            await Task.Delay(50);
        }

        XeAlarmAndConditionEventArgs? final;
        lock (receivedLock)
            final = LastOurAlarmSnapshot(received);

        final.Should().NotBeNull($"{because} (no matching alarm in buffer)");
        match(final!).Should().BeTrue(because);
        return final!;
    }

    [Fact]
    public async Task AlarmIntegration_RetainedActiveAlarm_IsReceived()
    {
        await using XeClient client = await OpcUaServerFixture.CreateConnectedClientAsync(
            OpcUaServerFixture.AlarmIntegrationOptions);

        var received = new List<XeAlarmAndConditionEventArgs>();
        object receivedLock = new();

        void OnAlarm(object? _, XeAlarmAndConditionEventArgs e)
        {
            lock (receivedLock)
                received.Add(e);
        }

        client.AlarmAndConditionEventReceived += OnAlarm;
        try
        {
            await Task.Delay(AcReadyDelay);

            received.Count.Should().BeGreaterThan(0, "expect ConditionRefresh / retained alarm from test server");
            XeAlarmAndConditionEventArgs retained = await WaitForIntegrationAsync(
                received,
                receivedLock,
                e => e.ActiveState == true
                    && e.Severity >= 800
                    && e.Message != null
                    && e.Message.Contains("integration alarm active", StringComparison.Ordinal),
                TimeSpan.FromSeconds(6),
                "expected retained active integration alarm");

            retained.EventTypeNodeId.Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            client.AlarmAndConditionEventReceived -= OnAlarm;
        }
    }

    [Fact]
    public async Task AlarmIntegration_Clear_ProducesInactiveNotification()
    {
        await using XeClient client = await OpcUaServerFixture.CreateConnectedClientAsync(
            OpcUaServerFixture.AlarmIntegrationOptions);

        var received = new List<XeAlarmAndConditionEventArgs>();
        object receivedLock = new();

        void OnAlarm(object? _, XeAlarmAndConditionEventArgs e)
        {
            lock (receivedLock)
                received.Add(e);
        }

        client.AlarmAndConditionEventReceived += OnAlarm;
        try
        {
            await Task.Delay(AcReadyDelay);

            _fixture.ClearIntegrationTestAlarm();
            await client.RequestAlarmConditionRefreshAsync();

            await WaitForIntegrationAsync(
                received,
                receivedLock,
                e => e.ActiveState == false
                    && e.Message != null
                    && e.Message.Contains("cleared", StringComparison.Ordinal)
                    && e.Severity <= 100,
                TimeSpan.FromSeconds(8),
                "expected cleared integration alarm");
        }
        finally
        {
            client.AlarmAndConditionEventReceived -= OnAlarm;
            _fixture.ClearIntegrationTestAlarm();
        }
    }

    [Fact]
    public async Task AlarmIntegration_RaiseAgain_ProducesActiveNotification()
    {
        await using XeClient client = await OpcUaServerFixture.CreateConnectedClientAsync(
            OpcUaServerFixture.AlarmIntegrationOptions);

        var received = new List<XeAlarmAndConditionEventArgs>();
        object receivedLock = new();

        void OnAlarm(object? _, XeAlarmAndConditionEventArgs e)
        {
            lock (receivedLock)
                received.Add(e);
        }

        client.AlarmAndConditionEventReceived += OnAlarm;
        try
        {
            await Task.Delay(AcReadyDelay);

            _fixture.ClearIntegrationTestAlarm();
            await client.RequestAlarmConditionRefreshAsync();
            await Task.Delay(TimeSpan.FromMilliseconds(200));

            _fixture.RaiseIntegrationTestAlarm();
            await client.RequestAlarmConditionRefreshAsync();

            XeAlarmAndConditionEventArgs raised = await WaitForIntegrationAsync(
                received,
                receivedLock,
                e => e.ActiveState == true
                    && e.Severity >= 800
                    && e.Message != null
                    && e.Message.Contains("integration alarm active", StringComparison.Ordinal),
                TimeSpan.FromSeconds(8),
                "expected integration alarm active again after raise");

            raised.ConditionName.Should().Be(TestNodeManager.IntegrationAlarmConditionName);
        }
        finally
        {
            client.AlarmAndConditionEventReceived -= OnAlarm;
            _fixture.ClearIntegrationTestAlarm();
        }
    }
}
