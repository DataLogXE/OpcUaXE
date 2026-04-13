# OpcUaXE.Client

.NET OPC UA client library with clean high-level API. No OPC UA SDK types in the public surface.

**Targets:** .NET 8 / .NET 9

## Features

- Connect by IP/port or endpoint (auto-discovery, security-level selection)
- Service mode with keep-alive and auto-reconnect
- Browse, Read, Write (single & batch, type coercion)
- Subscriptions (monitored items with configurable intervals)
- Alarms & Conditions (mapped event data, no SDK types)
- Certificate validation via events

## Quick Start

```csharp
using OpcUaXE.Client;

await using var client = new XeClient();

client.StateChanged += (_, s) => Console.WriteLine($"State: {s}");
client.CertificateValidationRequested += (_, e) => e.AcceptPermanently = true;

await client.ConnectAsync("192.168.1.100", 4840, new XeClientOptions
{
    ClientName = "MyApp"
});

// Read
var result = await client.ReadValueAsync("ns=2;s=MyVariable");
Console.WriteLine($"{result.Address}: {result.Value}");

// Write
await client.WriteValueAsync("ns=2;s=MyVariable", 42);

// Browse
var children = await client.BrowseAsync("i=84");

await client.DisconnectAsync();
```

### Service Mode (Auto-Reconnect)

```csharp
await using var client = new XeClient();

client.MonitoredItemValuesReceived += (_, items) =>
{
    foreach (var item in items) Console.WriteLine(item);
};

await client.StartServiceAsync("192.168.1.100", options: new XeClientOptions
{
    AutoReconnectDelay = TimeSpan.FromSeconds(5)
});

client.AddMonitoredItem("ns=2;s=MyVariable", samplingInterval: 100, publishInterval: 1000);

Console.ReadKey();
await client.StopServiceAsync();
```

## XeClientOptions

| Property | Default | Description |
|----------|---------|-------------|
| `ClientName` | `"OpcUaXEClient"` | Session name |
| `UserName` / `Password` | `""` | Credentials (empty = anonymous) |
| `KeepAliveTimeout` | `30s` | Keep-alive watchdog (`Zero` = off) |
| `AutoReconnectDelay` | `5s` | Reconnect delay in service mode (`Zero` = off) |
| `AlarmsAndConditionsEnable` | `true` | Enable A&C subscription |
| `AlarmsAndConditionsNodeId` | `"i=2253"` | Event-notifier node |
| `PreferredLocales` | `null` | BCP-47 locales, e.g. `["de", "en-US"]` |

## Events

| Event | Description |
|-------|-------------|
| `ServiceMessage` | Informational messages |
| `ServiceException` | Internal exceptions |
| `StateChanged` | Connection state changes |
| `CertificateValidationRequested` | Accept/reject server certificate |
| `KeepAliveReceived` | Keep-alive tick |
| `MonitoredItemValuesReceived` | New values for monitored items |
| `AlarmAndConditionEventReceived` | A&C event data |

## License

See [LICENSE](LICENSE).
