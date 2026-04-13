using System.Net;
using System.Net.Sockets;
using Opc.Ua;
using Opc.Ua.Configuration;
using OpcUaXE.Client;

namespace OpcUaXE.UnitTests.Infrastructure;

/// <summary>
/// xUnit collection fixture that starts a single in-process OPC UA test server
/// before the first test in the collection runs and stops it afterwards.
/// </summary>
public sealed class OpcUaServerFixture : IAsyncLifetime
{
    private readonly TestOpcUaServer _server = new();
    private string _certBasePath = null!;

    internal TestOpcUaServer Server => _server;

    public async Task InitializeAsync()
    {
        TestNodeIds.Port = GetFreeTcpPort();
        _certBasePath = Path.Combine(
            Path.GetTempPath(), "OpcUaXE_UnitTests", $"pki_{Guid.NewGuid():N}");
        ApplicationConfiguration config = await BuildServerConfigurationAsync();
        await _server.StartAsync(config, CancellationToken.None);
        await VerifyServerTcpListeningAsync(TestNodeIds.Port);
    }

    private static async Task VerifyServerTcpListeningAsync(int port)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    public async Task DisposeAsync()
    {
        await _server.StopAsync(CancellationToken.None);
    }

    // HELPER: BUILD A CONNECTED CLIENT FOR INTEGRATION TESTS ##########

    /// <summary>
    /// Creates and returns a fully connected <see cref="XeClient"/> pointed at the test server.
    /// The caller is responsible for disposing it (use <c>await using</c>).
    /// </summary>
    public static async Task<XeClient> CreateConnectedClientAsync(
        XeClientOptions? options = null, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        var client = new XeClient();
        client.CertificateValidationRequested += (_, e) => e.Accept = true;
        await client.ConnectAsync(TestNodeIds.Host, TestNodeIds.Port, options ?? DefaultOptions, ct: cts.Token);
        return client;
    }

    /// <summary>
    /// Default options used in integration tests: no A&amp;C, short keep-alive,
    /// no auto-reconnect so test failures are not masked by reconnects.
    /// </summary>
    public static XeClientOptions DefaultOptions => new()
    {
        AlarmsAndConditionsEnable = false,
        KeepAliveTimeout = TimeSpan.FromSeconds(15),
        AutoReconnectDelay = TimeSpan.Zero
    };

    /// <summary>Integration tests for <see cref="XeClientOptions.AlarmsAndConditionsEnable"/> against <see cref="TestNodeManager"/>.</summary>
    public static XeClientOptions AlarmIntegrationOptions => new()
    {
        AlarmsAndConditionsEnable = true,
        AlarmsAndConditionsNodeId = TestNodeIds.AlarmEventNotifier,
        KeepAliveTimeout = TimeSpan.FromSeconds(15),
        AutoReconnectDelay = TimeSpan.Zero
    };

    internal void RaiseIntegrationTestAlarm() =>
        _server.TestNodes?.RaiseIntegrationTestAlarm();

    internal void ClearIntegrationTestAlarm() =>
        _server.TestNodes?.ClearIntegrationTestAlarm();

    // PRIVATE: SERVER CONFIGURATION ##########

    private async Task<ApplicationConfiguration> BuildServerConfigurationAsync()
    {
        var config = new ApplicationConfiguration
        {
            ApplicationName = "OpcUaXE.UnitTests.Server",
            ApplicationUri = Utils.Format("urn:{0}:OpcUaXETestServer", System.Net.Dns.GetHostName()),
            ProductUri = "urn:OpcUaXETestServer",
            ApplicationType = ApplicationType.Server,
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = "Directory",
                    StorePath = Path.Combine(_certBasePath, "own"),
                    SubjectName = "CN=OpcUaXE.UnitTests.Server, C=DE"
                },
                TrustedIssuerCertificates = new CertificateTrustList
                {
                    StoreType = "Directory",
                    StorePath = Path.Combine(_certBasePath, "trusted", "issuers")
                },
                TrustedPeerCertificates = new CertificateTrustList
                {
                    StoreType = "Directory",
                    StorePath = Path.Combine(_certBasePath, "trusted", "certs")
                },
                RejectedCertificateStore = new CertificateTrustList
                {
                    StoreType = "Directory",
                    StorePath = Path.Combine(_certBasePath, "rejected", "certs")
                },
                AutoAcceptUntrustedCertificates = true,
                AddAppCertToTrustedStore = true
            },
            TransportConfigurations = [],
            TransportQuotas = new TransportQuotas { OperationTimeout = 15_000 },
            ServerConfiguration = new ServerConfiguration
            {
                BaseAddresses = [TestNodeIds.EndpointUrl],
                SecurityPolicies =
                [
                    new ServerSecurityPolicy
                    {
                        SecurityMode = MessageSecurityMode.None,
                        SecurityPolicyUri = SecurityPolicies.None
                    }
                ],
                UserTokenPolicies = [new UserTokenPolicy(UserTokenType.Anonymous)],
                DiagnosticsEnabled = false,
                MaxSessionCount = 100,
                MinSessionTimeout = 10_000,
                MaxSessionTimeout = 3_600_000,
                MaxBrowseContinuationPoints = 10,
                MaxQueryContinuationPoints = 10,
                MaxHistoryContinuationPoints = 100,
                MaxRequestAge = 600_000,
                MinPublishingInterval = 100,
                MaxPublishingInterval = 3_600_000,
                PublishingResolution = 50,
                MaxSubscriptionLifetime = 3_600_000,
                MaxMessageQueueSize = 10,
                MaxNotificationQueueSize = 1_000,
                MaxNotificationsPerPublish = 1_000,
                MinMetadataSamplingInterval = 1_000
            },
            TraceConfiguration = new TraceConfiguration
            {
                OutputFilePath = string.Empty,
                DeleteOnLoad = true,
                TraceMasks = 0
            }
        };

        await config.ValidateAsync(ApplicationType.Server);

        var application = new ApplicationInstance((ITelemetryContext?)null)
        {
            ApplicationName = config.ApplicationName,
            ApplicationType = ApplicationType.Server,
            ApplicationConfiguration = config
        };
        await application.CheckApplicationInstanceCertificatesAsync(false, 2048);

        return config;
    }
}

/// <summary>xUnit collection definition – shared across all integration test classes.</summary>
[CollectionDefinition("OpcUaServer")]
public sealed class OpcUaServerCollection : ICollectionFixture<OpcUaServerFixture> { }
