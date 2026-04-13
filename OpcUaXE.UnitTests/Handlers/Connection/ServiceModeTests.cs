using OpcUaXE.Client;
using OpcUaXE.Client.Exceptions;
using OpcUaXE.UnitTests.Infrastructure;

namespace OpcUaXE.UnitTests.Handlers.Connection;

[Collection("OpcUaServer")]
public sealed class ServiceModeTests
{
    // HELPERS ##########

    private static async Task WaitForStateAsync(
        XeClient client, Func<XeClientState, bool> predicate,
        TimeSpan timeout, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        while (!predicate(client.State))
        {
            cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(100, cts.Token);
        }
    }

    // TESTS ##########

    [Fact]
    public async Task StartServiceAsync_EventuallyConnects()
    {
        await using var client = new XeClient();
        client.CertificateValidationRequested += (_, e) => e.Accept = true;

        // StartServiceAsync is non-blocking – returns before Connected.
        await client.ConnectAsync(
            TestNodeIds.Host, TestNodeIds.Port, OpcUaServerFixture.DefaultOptions);

        await WaitForStateAsync(
            client, s => s == XeClientState.Connected, TimeSpan.FromSeconds(25));

        client.State.Should().Be(XeClientState.Connected);

        await client.DisconnectAsync();
    }

    [Fact]
    public async Task StopServiceAsync_AfterStart_StateEventuallyBecomesDisconnected()
    {
        await using var client = new XeClient();
        client.CertificateValidationRequested += (_, e) => e.Accept = true;

        await client.ConnectAsync(
            TestNodeIds.Host, TestNodeIds.Port, OpcUaServerFixture.DefaultOptions);

        // Wait until connected so the subsequent stop is meaningful.
        await WaitForStateAsync(
            client, s => s == XeClientState.Connected, TimeSpan.FromSeconds(25));

        await client.DisconnectAsync();

        // StopServiceAsync is non-blocking in service mode – wait for disconnected.
        await WaitForStateAsync(
            client, s => s == XeClientState.Disconnected, TimeSpan.FromSeconds(15));

        client.State.Should().Be(XeClientState.Disconnected);
    }

    [Fact]
    public async Task StartServiceAsync_WhenAlreadyConnecting_ThrowsXeConnectionException()
    {
        await using var client = new XeClient();
        client.CertificateValidationRequested += (_, e) => e.Accept = true;

        await client.ConnectAsync(
            TestNodeIds.Host, TestNodeIds.Port, OpcUaServerFixture.DefaultOptions);

        // Wait until the background loop has moved the state away from Disconnected.
        await WaitForStateAsync(
            client, s => s != XeClientState.Disconnected, TimeSpan.FromSeconds(25));

        // A second StartServiceAsync while already running must throw.
        Func<Task> act = () => client.ConnectAsync(
            TestNodeIds.Host, TestNodeIds.Port, OpcUaServerFixture.DefaultOptions);

        await act.Should().ThrowAsync<XeConnectionException>();

        await client.DisconnectAsync();
        await WaitForStateAsync(
            client, s => s == XeClientState.Disconnected, TimeSpan.FromSeconds(15));
    }
}
