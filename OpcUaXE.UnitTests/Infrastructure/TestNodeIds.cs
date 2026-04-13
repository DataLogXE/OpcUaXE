namespace OpcUaXE.UnitTests.Infrastructure;

/// <summary>
/// Well-known node addresses and server configuration constants used across all integration tests.
/// All node IDs use namespace index 2 which corresponds to the custom test namespace
/// "http://opcuaxe.test/" registered by <see cref="TestNodeManager"/>.
/// </summary>
internal static class TestNodeIds
{
    /// <summary>Assigned by <see cref="OpcUaServerFixture"/> before the in-process server starts.</summary>
    public static int Port { get; internal set; } = 4841;

    /// <summary>Use IPv4 loopback so discovery matches the server base address (avoids IPv4/IPv6 mismatch).</summary>
    public const string Host = "127.0.0.1";

    public static string EndpointUrl => $"opc.tcp://{Host}:{Port}";

    // READABLE / WRITABLE VARIABLE NODES ##########

    /// <summary>Boolean read/write node.</summary>
    public const string BoolNode = "ns=2;s=BoolNode";

    /// <summary>Int32 read/write node.</summary>
    public const string Int32Node = "ns=2;s=Int32Node";

    /// <summary>Double read/write node.</summary>
    public const string DoubleNode = "ns=2;s=DoubleNode";

    /// <summary>String read/write node.</summary>
    public const string StringNode = "ns=2;s=StringNode";

    // SUBSCRIPTION TEST NODE ##########

    /// <summary>Int32 node whose value changes during subscription tests.</summary>
    public const string MonitorNode = "ns=2;s=MonitorNode";

    // ALARMS & CONDITIONS (see <see cref="TestNodeManager"/>) ##########

    /// <summary>Object with <c>EventNotifier = SubscribeToEvents</c> hosting <see cref="TestNodeManager.IntegrationAlarmConditionName"/>.</summary>
    public const string AlarmEventNotifier = "ns=2;s=AlarmEventSource";
}
