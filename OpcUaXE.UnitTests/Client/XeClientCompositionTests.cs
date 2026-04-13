using System.Threading;
using OpcUaXE.Client;
using OpcUaXE.Client.Core;
using OpcUaXE.Client.Core.Handlers;
using OpcUaXE.Client.Core.Helper;
using OpcUaXE.Client.Events;
using OpcUaXE.Client.Types;

namespace OpcUaXE.UnitTests.Client;

/// <summary>
/// Tests that <see cref="XeClient"/> correctly delegates to its handlers
/// and re-raises handler events, without requiring a live OPC UA server.
/// </summary>
public sealed class XeClientCompositionTests : IAsyncDisposable
{
    private readonly XeClientContext _ctx;
    private readonly StubConnectionHandler _connection;
    private readonly StubReadWriteHandler _readWrite;
    private readonly StubSubscriptionHandler _subscriptions;
    private readonly StubAlarmsHandler _alarms;
    private readonly StubBrowseHandler _browser;
    private readonly XeClient _client;

    public XeClientCompositionTests()
    {
        _ctx = new XeClientContext();
        _connection = new StubConnectionHandler();
        _readWrite = new StubReadWriteHandler();
        _subscriptions = new StubSubscriptionHandler();
        _alarms = new StubAlarmsHandler();
        _browser = new StubBrowseHandler();

        _client = new XeClient(
            _ctx,
            _ => _connection,
            _ => _readWrite,
            _ => _subscriptions,
            _ => _alarms,
            _ => _browser);
    }

    public async ValueTask DisposeAsync() => await _client.DisposeAsync();

    // STATE ##########

    [Fact]
    public void State_ReflectsContextState()
    {
        _ctx.SetState(XeClientState.Connected);

        _client.State.Should().Be(XeClientState.Connected);
        _client.IsConnected.Should().BeTrue();
    }

    [Fact]
    public void StateChanged_IsRaised_WhenContextStateChanges()
    {
        XeClientState? raised = null;
        _client.StateChanged += (_, e) => raised = e.State;

        _ctx.SetState(XeClientState.Connected);

        raised.Should().Be(XeClientState.Connected);
    }

    [Fact]
    public void ServiceMessage_IsRaised_WhenContextRaisesMessage()
    {
        string? received = null;
        _client.ServiceMessage += (_, e) => received = e.Message;

        _ctx.RaiseServiceMessage("test-message");

        received.Should().Be("test-message");
    }

    [Fact]
    public void ServiceException_IsRaised_WhenContextRaisesException()
    {
        Exception? received = null;
        _client.ServiceException += (_, e) => received = e.Exception;
        var ex = new InvalidOperationException("boom");

        _ctx.RaiseServiceException(ex);

        received.Should().BeSameAs(ex);
    }

    // CONNECTION ##########

    [Fact]
    public async Task StartServiceAsync_ByIpAndPort_DelegatesToConnectionHandler()
    {
        await _client.ConnectAsync("10.0.0.1", 4840);

        _connection.LastInfo.Should().NotBeNull();
        _connection.LastInfo!.IpAddress.Should().Be("10.0.0.1");
        _connection.LastInfo.Port.Should().Be(4840);
        _connection.LastInfo.Blocking.Should().BeTrue();
    }

    [Fact]
    public async Task StartServiceAsync_SetsServiceMode_True()
    {
        await _client.ConnectAsync("10.0.0.1", 4840);

        _connection.LastInfo!.Blocking.Should().BeTrue();
    }

    [Fact]
    public async Task StopServiceAsync_DelegatesToConnectionHandler()
    {
        await _client.DisconnectAsync();

        _connection.DisconnectCalled.Should().BeTrue();
    }

    [Fact]
    public async Task BrowseServerEndpointsAsync_DelegatesToConnectionHandler()
    {
        var result = await _client.BrowseServerEndpointsAsync("10.0.0.1", 4840);

        _connection.BrowseEndpointsCalled.Should().BeTrue();
        result.Should().BeEmpty();
    }

    [Fact]
    public void CertificateValidationRequested_IsRaised_WhenConnectionHandlerRaisesIt()
    {
        XeCertificateValidationEventArgs? received = null;
        _client.CertificateValidationRequested += (_, e) => received = e;

        var args = StubCertificateArgs();
        _connection.RaiseCertificateValidationRequested(args);

        received.Should().BeSameAs(args);
    }

    [Fact]
    public void KeepAliveReceived_IsRaised_WhenConnectionHandlerRaisesIt()
    {
        XeKeepAliveEventArgs? received = null;
        _client.KeepAliveReceived += (_, e) => received = e;

        var args = new XeKeepAliveEventArgs(DateTime.UtcNow);
        _connection.RaiseKeepAliveReceived(args);

        received.Should().BeSameAs(args);
    }

    // READ / WRITE ##########

    [Fact]
    public async Task ReadValueAsync_DelegatesToReadWriteHandler()
    {
        await _client.ReadValueAsync("ns=2;s=TestNode");

        _readWrite.LastReadAddress.Should().Be("ns=2;s=TestNode");
    }

    [Fact]
    public async Task WriteValueAsync_ByAddress_DelegatesToReadWriteHandler()
    {
        await _client.WriteValueAsync("ns=2;s=TestNode", 42);

        _readWrite.LastWriteAddress.Should().Be("ns=2;s=TestNode");
        _readWrite.LastWriteValue.Should().Be(42);
    }

    [Fact]
    public async Task WriteValuesAsync_DelegatesToReadWriteHandler()
    {
        var items = new List<XeWriteItem> { new("ns=2;s=Node", 1) };

        await _client.WriteValuesAsync(items);

        _readWrite.WriteValuesCalled.Should().BeTrue();
    }

    // SUBSCRIPTIONS ##########

    [Fact]
    public void AddMonitoredItem_DelegatesToSubscriptionHandler()
    {
        _client.AddMonitoredItem("ns=2;s=Tag", 100, 1000);

        _subscriptions.LastAddedAddress.Should().Be("ns=2;s=Tag");
        _subscriptions.LastSamplingMs.Should().Be(100);
        _subscriptions.LastPublishMs.Should().Be(1000);
    }

    [Fact]
    public void RemoveMonitoredItem_DelegatesToSubscriptionHandler()
    {
        _client.RemoveMonitoredItem("ns=2;s=Tag");

        _subscriptions.LastRemovedAddress.Should().Be("ns=2;s=Tag");
    }

    [Fact]
    public void RemoveAllMonitoredItems_DelegatesToSubscriptionHandler()
    {
        _client.RemoveAllMonitoredItems();

        _subscriptions.RemoveAllCalled.Should().BeTrue();
    }

    [Fact]
    public void MonitoredItemValuesReceived_IsRaised_WhenSubscriptionHandlerRaisesIt()
    {
        IReadOnlyList<XeReadItem>? received = null;
        _client.MonitoredItemValuesReceived += (_, items) => received = items;

        var batch = new List<XeReadItem>();
        _subscriptions.RaiseMonitoredItemValuesReceived(batch);

        received.Should().BeSameAs(batch);
    }

    // BROWSER ##########

    [Fact]
    public async Task BrowseRootAsync_DelegatesToBrowseHandler()
    {
        await _client.BrowseRootAsync();

        _browser.BrowseRootCalled.Should().BeTrue();
    }

    [Fact]
    public async Task BrowseAsync_ByString_DelegatesToBrowseHandler()
    {
        await _client.BrowseAsync("i=84");

        _browser.LastBrowsedAddress.Should().Be("i=84");
    }

    // ALARMS ##########

    [Fact]
    public void AlarmAndConditionEventReceived_IsRaised_WhenAlarmsHandlerRaisesIt()
    {
        XeAlarmAndConditionEventArgs? received = null;
        _client.AlarmAndConditionEventReceived += (_, e) => received = e;

        var args = new XeAlarmAndConditionEventArgs();
        _alarms.RaiseAlarmAndConditionEventReceived(args);

        received.Should().BeSameAs(args);
    }

    [Fact]
    public async Task Constructor_StartsAlarmsBackgroundLoops()
    {
        var stub = new StubAlarmsHandler();
        await using var client = new XeClient(
            new XeClientContext(),
            _ => new StubConnectionHandler(),
            _ => new StubReadWriteHandler(),
            _ => new StubSubscriptionHandler(),
            _ => stub,
            _ => new StubBrowseHandler());

        for (int i = 0; i < 100 && (stub.AlarmsLoopStartedCount == 0 || stub.NotificationLoopStartedCount == 0); i++)
            await Task.Delay(20);

        stub.AlarmsLoopStartedCount.Should().BeGreaterThan(0);
        stub.NotificationLoopStartedCount.Should().BeGreaterThan(0);
    }

    // DISPOSAL ##########

    [Fact]
    public async Task DisposeAsync_CompletesWithoutException()
    {
        var client = new XeClient(
            new XeClientContext(),
            _ => new StubConnectionHandler(),
            _ => new StubReadWriteHandler(),
            _ => new StubSubscriptionHandler(),
            _ => new StubAlarmsHandler(),
            _ => new StubBrowseHandler());

        var act = async () => await client.DisposeAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_DoesNotThrow()
    {
        var client = new XeClient(
            new XeClientContext(),
            _ => new StubConnectionHandler(),
            _ => new StubReadWriteHandler(),
            _ => new StubSubscriptionHandler(),
            _ => new StubAlarmsHandler(),
            _ => new StubBrowseHandler());

        await client.DisposeAsync();
        var act = async () => await client.DisposeAsync();

        await act.Should().NotThrowAsync();
    }

    // HELPERS ##########

    private static XeCertificateValidationEventArgs StubCertificateArgs()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(1024);
        var req = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            "CN=UnitTest", rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
        return new XeCertificateValidationEventArgs(cert);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// MANUAL TEST STUBS
// ═══════════════════════════════════════════════════════════════════════════════

internal sealed class StubConnectionHandler : IConnectionHandler
{
    public event EventHandler<XeCertificateValidationEventArgs>? CertificateValidationRequested;
    public event EventHandler<XeKeepAliveEventArgs>? KeepAliveReceived;

    public XeConnectionInfo? LastInfo { get; private set; }
    public bool DisconnectCalled { get; private set; }
    public bool BrowseEndpointsCalled { get; private set; }

    public Task ConnectAsync(XeConnectionInfo info, XeClientOptions? opts, CancellationToken ct)
    {
        LastInfo = info;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken ct)
    {
        DisconnectCalled = true;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<XeConnectionEndpoint>> BrowseServerEndpointsAsync(
        string ip, int port, CancellationToken ct)
    {
        BrowseEndpointsCalled = true;
        return Task.FromResult<IReadOnlyList<XeConnectionEndpoint>>([]);
    }

    public Task ConnectionLoopAsync() => Task.CompletedTask;

    public void RaiseCertificateValidationRequested(XeCertificateValidationEventArgs args) =>
        CertificateValidationRequested?.Invoke(this, args);

    public void RaiseKeepAliveReceived(XeKeepAliveEventArgs args) =>
        KeepAliveReceived?.Invoke(this, args);
}

internal sealed class StubReadWriteHandler : IReadWriteHandler
{
    public string? LastReadAddress { get; private set; }
    public string? LastWriteAddress { get; private set; }
    public object? LastWriteValue { get; private set; }
    public bool WriteValuesCalled { get; private set; }

    public Task<XeReadItem> ReadValueAsync(string nodeAddress, CancellationToken ct)
    {
        LastReadAddress = nodeAddress;
        return Task.FromResult(new XeReadItem(new XeNodeAddress(nodeAddress)));
    }

    public Task<IReadOnlyList<XeReadItem>> ReadValuesAsync(
        IReadOnlyList<string> nodeAddresses, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<XeReadItem>>([]);

    public Task WriteValueAsync(XeWriteItem item, CancellationToken ct) =>
        Task.CompletedTask;

    public Task<XeWriteItem> WriteValueAsync(string nodeAddress, object value, CancellationToken ct)
    {
        LastWriteAddress = nodeAddress;
        LastWriteValue = value;
        return Task.FromResult(new XeWriteItem(nodeAddress, value));
    }

    public Task WriteValuesAsync(IReadOnlyList<XeWriteItem> items, CancellationToken ct)
    {
        WriteValuesCalled = true;
        return Task.CompletedTask;
    }
}

internal sealed class StubSubscriptionHandler : ISubscriptionHandler
{
    public event EventHandler<IReadOnlyList<XeReadItem>>? MonitoredItemValuesReceived;

    public string? LastAddedAddress { get; private set; }
    public int LastSamplingMs { get; private set; }
    public int LastPublishMs { get; private set; }
    public string? LastRemovedAddress { get; private set; }
    public bool RemoveAllCalled { get; private set; }

    public void AddMonitoredItem(string nodeAddress, int samplingMs, int publishMs)
    {
        LastAddedAddress = nodeAddress;
        LastSamplingMs = samplingMs;
        LastPublishMs = publishMs;
    }

    public void AddMonitoredItems(IReadOnlyList<XeMonitoredItem> items) { }

    public void RemoveMonitoredItem(string nodeAddress) =>
        LastRemovedAddress = nodeAddress;

    public void RemoveMonitoredItems(IReadOnlyList<string> nodeAddresses) { }

    public void RemoveAllMonitoredItems() => RemoveAllCalled = true;

    public Task SyncLoopAsync() => Task.CompletedTask;
    public Task ConsumerLoopAsync() => Task.CompletedTask;

    public void RaiseMonitoredItemValuesReceived(IReadOnlyList<XeReadItem> items) =>
        MonitoredItemValuesReceived?.Invoke(this, items);
}

internal sealed class StubAlarmsHandler : IAlarmsHandler
{
    public event EventHandler<XeAlarmAndConditionEventArgs>? AlarmAndConditionEventReceived;

    public int AlarmsLoopStartedCount;
    public int NotificationLoopStartedCount;

    public Task AlarmsLoopAsync()
    {
        Interlocked.Increment(ref AlarmsLoopStartedCount);
        return Task.CompletedTask;
    }

    public Task NotificationLoopAsync()
    {
        Interlocked.Increment(ref NotificationLoopStartedCount);
        return Task.CompletedTask;
    }

    public Task RequestConditionRefreshAsync(CancellationToken ct = default) => Task.CompletedTask;

    public void RaiseAlarmAndConditionEventReceived(XeAlarmAndConditionEventArgs args) =>
        AlarmAndConditionEventReceived?.Invoke(this, args);
}

internal sealed class StubBrowseHandler : IBrowseHandler
{
    public string? LastBrowsedAddress { get; private set; }
    public bool BrowseRootCalled { get; private set; }

    public Task<IReadOnlyList<XeBrowseResultItem>> BrowseRootAsync(CancellationToken ct)
    {
        BrowseRootCalled = true;
        return Task.FromResult<IReadOnlyList<XeBrowseResultItem>>([]);
    }

    public Task<IReadOnlyList<XeBrowseResultItem>> BrowseAsync(
        string nodeAddress, CancellationToken ct)
    {
        LastBrowsedAddress = nodeAddress;
        return Task.FromResult<IReadOnlyList<XeBrowseResultItem>>([]);
    }
}
