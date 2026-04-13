namespace OpcUaXE.Client;

/// <summary>
/// Immutable configuration snapshot passed to <see cref="XeClient"/> connection methods.
/// Use C# object-initializer syntax; all properties are <c>init</c>-only.
/// </summary>
public sealed class XeClientOptions
{
    /// <summary>OPC UA session name sent to the server. Defaults to <c>"OpcUaXEClient"</c>.</summary>
    public string ClientName { get; init; } = "OpcUaXEClient";

    /// <summary>
    /// Username for server authentication.
    /// An empty string selects anonymous login (the default).
    /// </summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>
    /// Password for server authentication. Ignored when <see cref="UserName"/> is empty.
    /// </summary>
    /// <remarks>
    /// <b>Security notice:</b> The password is stored as a plain <see cref="string"/> and
    /// therefore remains readable in managed-heap memory dumps for the lifetime of this object.
    /// For high-security scenarios, avoid long-lived <see cref="XeClientOptions"/> instances
    /// and consider zeroing sensitive fields after use via a dedicated wrapper.
    /// </remarks>
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// Keep-alive watchdog timeout. The session is considered lost when no keep-alive is received within
    /// this interval. Use <see cref="TimeSpan.Zero"/> to disable the watchdog.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan KeepAliveTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Delay before the next automatic reconnect attempt after a connection loss.
    /// Use <see cref="TimeSpan.Zero"/> to disable automatic reconnection.
    /// Defaults to 5 seconds.
    /// </summary>
    public TimeSpan AutoReconnectDelay { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Enables OPC UA Alarms and Conditions subscription.
    /// Changes take effect after the next (re)connect.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool AlarmsAndConditionsEnable { get; init; } = false;

    /// <summary>
    /// Node ID of the event-notifier object (default: Server object <c>i=2253</c>).
    /// Used when <see cref="AlarmsAndConditionsEnable"/> is <see langword="true"/>.
    /// </summary>
    public string AlarmsAndConditionsNodeId { get; init; } = "i=2253";

    /// <summary>
    /// Interval at which <c>ConditionType.ConditionRefresh</c> is called automatically while
    /// the A&amp;C subscription is running. Use <see cref="TimeSpan.Zero"/> to disable the
    /// periodic refresh. Defaults to <see cref="TimeSpan.Zero"/> (disabled).
    /// </summary>
    public TimeSpan AlarmConditionRefreshInterval { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Preferred session locales as BCP-47 tags (e.g. <c>"de"</c>, <c>"en-US"</c>).
    /// <see langword="null"/> falls back to <see cref="System.Globalization.CultureInfo.CurrentUICulture"/>.
    /// </summary>
    public IReadOnlyList<string>? PreferredLocales { get; init; }
}
