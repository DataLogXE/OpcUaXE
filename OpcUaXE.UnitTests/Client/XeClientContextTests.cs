using OpcUaXE.Client;
using OpcUaXE.Client.Core;
using OpcUaXE.Client.Events;

namespace OpcUaXE.UnitTests.Client;

public sealed class XeClientContextTests
{
    // STATE ##########

    [Fact]
    public void State_InitialValue_IsDisconnected()
    {
        using var ctx = new XeClientContext();

        ctx.State.Should().Be(XeClientState.Disconnected);
    }

    [Fact]
    public void IsConnected_WhenStateIsConnected_ReturnsTrue()
    {
        using var ctx = new XeClientContext();

        ctx.SetState(XeClientState.Connected);

        ctx.IsConnected.Should().BeTrue();
    }

    [Theory]
    [InlineData(XeClientState.Disconnected)]
    [InlineData(XeClientState.Connecting)]
    [InlineData(XeClientState.Disconnecting)]
    [InlineData(XeClientState.Error)]
    public void IsConnected_WhenStateIsNotConnected_ReturnsFalse(XeClientState state)
    {
        using var ctx = new XeClientContext();

        ctx.SetState(state);

        ctx.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void SetState_RaisesStateChangedEvent_WithNewState()
    {
        using var ctx = new XeClientContext();
        XeClientState? raised = null;
        ctx.StateChanged += (_, e) => raised = e.State;

        ctx.SetState(XeClientState.Connected);

        raised.Should().Be(XeClientState.Connected);
    }

    [Fact]
    public void SetState_RaisesStateChangedEvent_MultipleTimes()
    {
        using var ctx = new XeClientContext();
        var states = new List<XeClientState>();
        ctx.StateChanged += (_, e) => states.Add(e.State);

        ctx.SetState(XeClientState.Connecting);
        ctx.SetState(XeClientState.Connected);
        ctx.SetState(XeClientState.Disconnecting);
        ctx.SetState(XeClientState.Disconnected);

        states.Should().ContainInOrder(
            XeClientState.Connecting,
            XeClientState.Connected,
            XeClientState.Disconnecting,
            XeClientState.Disconnected);
    }

    // OPTIONS ##########

    [Fact]
    public void Options_InitialValue_UsesDefaults()
    {
        using var ctx = new XeClientContext();

        ctx.Options.ClientName.Should().Be("OpcUaXEClient");
    }

    [Fact]
    public void SetOptions_UpdatesOptions()
    {
        using var ctx = new XeClientContext();
        var opts = new XeClientOptions { ClientName = "TestClient" };

        ctx.SetOptions(opts);

        ctx.Options.ClientName.Should().Be("TestClient");
    }

    // SERVICE EVENTS ##########

    [Fact]
    public void RaiseServiceMessage_InvokesServiceMessageEvent()
    {
        using var ctx = new XeClientContext();
        XeServiceMessageEventArgs? received = null;
        ctx.ServiceMessage += (_, e) => received = e;

        ctx.RaiseServiceMessage("hello");

        received.Should().NotBeNull();
        received!.Message.Should().Be("hello");
    }

    [Fact]
    public void RaiseServiceMessage_WithNoSubscribers_DoesNotThrow()
    {
        using var ctx = new XeClientContext();

        var act = () => ctx.RaiseServiceMessage("no subscriber");

        act.Should().NotThrow();
    }

    [Fact]
    public void RaiseServiceException_InvokesServiceExceptionEvent()
    {
        using var ctx = new XeClientContext();
        XeServiceExceptionEventArgs? received = null;
        ctx.ServiceException += (_, e) => received = e;

        var ex = new InvalidOperationException("test");
        ctx.RaiseServiceException(ex);

        received.Should().NotBeNull();
        received!.Exception.Should().BeSameAs(ex);
    }

    // LIFETIME ##########

    [Fact]
    public void LifetimeToken_BeforeCancel_IsNotCancelled()
    {
        using var ctx = new XeClientContext();

        ctx.LifetimeToken.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public void Cancel_CancelsLifetimeToken()
    {
        var ctx = new XeClientContext();

        ctx.Cancel();

        ctx.LifetimeToken.IsCancellationRequested.Should().BeTrue();
        ctx.Dispose();
    }

    // SESSION ##########

    [Fact]
    public void Session_InitialValue_IsNull()
    {
        using var ctx = new XeClientContext();

        ctx.Session.Should().BeNull();
    }

    // UTILITY ##########

    [Fact]
    public void GetElapsed_AfterShortDelay_ReturnsPositiveTimeSpan()
    {
        long ts = System.Diagnostics.Stopwatch.GetTimestamp();

        TimeSpan elapsed = XeClientContext.GetElapsed(ts);

        elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void AcSubscriptionName_IsNonEmpty()
    {
        XeClientContext.AcSubscriptionName.Should().NotBeNullOrWhiteSpace();
    }
}
