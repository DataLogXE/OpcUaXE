using OpcUaXE.Client;

namespace OpcUaXE.UnitTests.Client;

public sealed class XeClientOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new XeClientOptions();

        options.ClientName.Should().Be("OpcUaXEClient");
        options.UserName.Should().BeEmpty();
        options.Password.Should().BeEmpty();
        options.KeepAliveTimeout.Should().Be(TimeSpan.FromSeconds(30));
        options.AutoReconnectDelay.Should().Be(TimeSpan.FromSeconds(5));
        options.AlarmsAndConditionsEnable.Should().BeFalse();
        options.AlarmsAndConditionsNodeId.Should().Be("i=2253");
        options.AlarmConditionRefreshInterval.Should().Be(TimeSpan.Zero);
        options.PreferredLocales.Should().BeNull();
    }

    [Fact]
    public void InitProperties_CanBeOverridden()
    {
        var options = new XeClientOptions
        {
            ClientName = "MyApp",
            UserName = "admin",
            Password = "secret",
            KeepAliveTimeout = TimeSpan.FromSeconds(10),
            AutoReconnectDelay = TimeSpan.Zero,
            AlarmsAndConditionsEnable = false,
            AlarmsAndConditionsNodeId = "ns=2;s=AlarmNode",
            AlarmConditionRefreshInterval = TimeSpan.FromMinutes(2),
            PreferredLocales = ["de", "en"]
        };

        options.ClientName.Should().Be("MyApp");
        options.UserName.Should().Be("admin");
        options.Password.Should().Be("secret");
        options.KeepAliveTimeout.Should().Be(TimeSpan.FromSeconds(10));
        options.AutoReconnectDelay.Should().Be(TimeSpan.Zero);
        options.AlarmsAndConditionsEnable.Should().BeFalse();
        options.AlarmsAndConditionsNodeId.Should().Be("ns=2;s=AlarmNode");
        options.AlarmConditionRefreshInterval.Should().Be(TimeSpan.FromMinutes(2));
        options.PreferredLocales.Should().BeEquivalentTo(new[] { "de", "en" });
    }
}
