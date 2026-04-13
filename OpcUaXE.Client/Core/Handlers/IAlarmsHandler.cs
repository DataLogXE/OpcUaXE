using OpcUaXE.Client.Events;

namespace OpcUaXE.Client.Core.Handlers;

/// <summary>Manages the OPC UA Alarms and Conditions event-notifier subscription.</summary>
internal interface IAlarmsHandler
{
    /// <summary>Raised when an OPC UA Alarms and Conditions event is received.</summary>
    event EventHandler<XeAlarmAndConditionEventArgs>? AlarmAndConditionEventReceived;

    /// <summary>Background loop that drives the A&amp;C state machine.</summary>
    Task AlarmsLoopAsync();

    /// <summary>Background loop that drains the notification channel and raises events.</summary>
    Task NotificationLoopAsync();

    /// <summary>Calls OPC UA <c>ConditionType.ConditionRefresh</c> for the A&amp;C subscription when registered.</summary>
    Task RequestConditionRefreshAsync(CancellationToken ct = default);
}
