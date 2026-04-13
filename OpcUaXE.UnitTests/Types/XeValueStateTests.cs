using OpcUaXE.Client.Types;

namespace OpcUaXE.UnitTests.Types;

public sealed class XeValueStateTests
{
    [Fact]
    public void Good_ReturnsGoodState()
    {
        var state = XeValueState.Good();

        state.IsGood.Should().BeTrue();
        state.IsBad.Should().BeFalse();
        state.IsUncertain.Should().BeFalse();
    }

    [Fact]
    public void Bad_ReturnsBadState()
    {
        var state = XeValueState.Bad();

        state.IsGood.Should().BeFalse();
        state.IsBad.Should().BeTrue();
        state.IsUncertain.Should().BeFalse();
    }

    [Fact]
    public void Uncertain_ReturnsUncertainState()
    {
        var state = XeValueState.Uncertain();

        state.IsGood.Should().BeFalse();
        state.IsBad.Should().BeFalse();
        state.IsUncertain.Should().BeTrue();
    }

    [Fact]
    public void ToString_ReturnsNonEmptyString()
    {
        XeValueState.Good().ToString().Should().NotBeNullOrWhiteSpace();
        XeValueState.Bad().ToString().Should().NotBeNullOrWhiteSpace();
        XeValueState.Uncertain().ToString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void StatusCodeValue_IsNonZeroForBad()
    {
        XeValueState.Bad().StatusCodeValue.Should().BeGreaterThan(0);
    }
}
