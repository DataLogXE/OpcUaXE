using OpcUaXE.Client;
using OpcUaXE.Client.Exceptions;
using OpcUaXE.UnitTests.Infrastructure;

namespace OpcUaXE.UnitTests.Handlers.Connection;

[Collection("OpcUaServer")]
public sealed class ConnectionTests
{
    // CONNECT / DISCONNECT ##########

    [Fact]
    public async Task StartServiceAsync_Succeeds_StateIsConnected()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        client.State.Should().Be(XeClientState.Connected);
        client.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task StopServiceAsync_AfterStartService_StateIsDisconnected()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        await client.DisconnectAsync();

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (client.State != XeClientState.Disconnected)
            await Task.Delay(50, cts.Token);

        client.State.Should().Be(XeClientState.Disconnected);
        client.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task StartServiceAsync_WhenAlreadyConnected_ThrowsXeConnectionException()
    {
        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        Func<Task> act = () => client.ConnectAsync(
            TestNodeIds.Host, TestNodeIds.Port, OpcUaServerFixture.DefaultOptions);

        await act.Should().ThrowAsync<XeConnectionException>();
    }

    // STATECHANGED EVENT ##########

    [Fact]
    public async Task StateChanged_IsFired_DuringConnect()
    {
        var states = new List<XeClientState>();
        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var client = new XeClient();
        client.CertificateValidationRequested += (_, e) => e.Accept = true;
        client.StateChanged += (_, e) =>
        {
            states.Add(e.State);
            if (e.State == XeClientState.Connected) connected.TrySetResult();
        };

        await client.ConnectAsync(
            TestNodeIds.Host, TestNodeIds.Port, OpcUaServerFixture.DefaultOptions);

        await connected.Task.WaitAsync(TimeSpan.FromSeconds(10));

        states.Should().Contain(XeClientState.Connecting);
        states.Should().Contain(XeClientState.Connected);
    }

    [Fact]
    public async Task StateChanged_IsFired_DuringDisconnect()
    {
        var states = new List<XeClientState>();
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var client = await OpcUaServerFixture.CreateConnectedClientAsync();
        client.StateChanged += (_, e) =>
        {
            states.Add(e.State);
            if (e.State == XeClientState.Disconnected) disconnected.TrySetResult();
        };

        await client.DisconnectAsync();

        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(10));

        states.Should().Contain(XeClientState.Disconnected);
    }

    // BROWSESERVERENDPOINTS ##########

    [Fact]
    public async Task BrowseServerEndpointsAsync_ReturnsAtLeastOneEndpoint()
    {
        await using var client = new XeClient();

        var endpoints = await client.BrowseServerEndpointsAsync(
            TestNodeIds.Host, TestNodeIds.Port);

        endpoints.Should().NotBeEmpty();
    }

    // DISPOSE ##########

    [Fact]
    public async Task DisposeAsync_WhileConnected_CompletesGracefully()
    {
        var client = await OpcUaServerFixture.CreateConnectedClientAsync();

        Func<Task> act = async () => await client.DisposeAsync();

        await act.Should().NotThrowAsync();
    }
}
