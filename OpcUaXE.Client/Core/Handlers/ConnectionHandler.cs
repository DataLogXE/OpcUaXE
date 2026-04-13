using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Client.ComplexTypes;
using Opc.Ua.Configuration;
using OpcUaXE.Client.Core.Helper;
using OpcUaXE.Client.Events;
using OpcUaXE.Client.Exceptions;
using OpcUaXE.Client.Types;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace OpcUaXE.Client.Core.Handlers;

/// <summary>
/// Manages the OPC UA session lifecycle: endpoint discovery, connection,
/// keep-alive monitoring, certificate validation, and auto-reconnect.
/// </summary>
internal sealed class ConnectionHandler : IConnectionHandler
{
    private static readonly ISessionFactory SessionFactory = new DefaultSessionFactory(null!);

    private readonly XeClientContext _ctx;

    public event EventHandler<XeCertificateValidationEventArgs>? CertificateValidationRequested;
    public event EventHandler<XeKeepAliveEventArgs>? KeepAliveReceived;

    private readonly object _connectLock = new();
    private volatile bool _connect;
    private XeConnectionInfo _connectionInfo = new();
    private ApplicationConfiguration _appConfig = null!;
    private long _lastKeepAliveTimestamp;

    public ConnectionHandler(XeClientContext ctx) => _ctx = ctx;

    #region Public API

    public async Task ConnectAsync(
        XeConnectionInfo connectionInfo, XeClientOptions? options, CancellationToken ct)
    {
        lock (_connectLock)
        {
            if (_ctx.State != XeClientState.Disconnected)
                throw new XeConnectionException(
                    "Client is already connected or connecting. Call StopServiceAsync first.");

            _ctx.SetOptions(options ?? new XeClientOptions());
            _connectionInfo = connectionInfo;
            _connect = true;
        }

        if (connectionInfo.Blocking)
            await WaitForStateAsync(ct, XeClientState.Connected).ConfigureAwait(false);
    }

    public async Task DisconnectAsync(CancellationToken ct)
    {
        bool blocking;
        lock (_connectLock)
        {
            blocking = _connectionInfo.Blocking;
            _connectionInfo = new XeConnectionInfo();
            _connect = false;
        }

        if (!blocking && _ctx.State != XeClientState.Disconnected)
            await WaitForStateAsync(ct, XeClientState.Disconnected).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<XeConnectionEndpoint>> BrowseServerEndpointsAsync(
        string ipAddress, int port, CancellationToken ct)
    {
        return await BrowseServerEndpointsInternalAsync(ipAddress, port, ct).ConfigureAwait(false);
    }

    #endregion

    #region Background Loop

    public async Task ConnectionLoopAsync()
    {
        while (!_ctx.LifetimeToken.IsCancellationRequested)
        {
            try
            {
                if (_connect)
                    await StartConnectionAsync().ConfigureAwait(false);

                await Task.Delay(100, _ctx.LifetimeToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_ctx.LifetimeToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _ctx.RaiseServiceException(ex);
            }
        }
    }

    #endregion

    #region Connection State Machine

    private async Task StartConnectionAsync()
    {
        _ctx.SetState(XeClientState.Connecting);

        while (_ctx.State == XeClientState.Connecting)
        {
            ISession? session = null;
            try
            {
                session = await CreateClientSessionAsync().ConfigureAwait(false);
                _ctx.Session = session;

                session.KeepAlive -= SessionKeepAlive;
                session.KeepAlive += SessionKeepAlive;

                if (_ctx.State == XeClientState.Connecting)
                {
                    await session.ReadNodeAsync(
                            new NodeId(VariableIds.Server_ServerStatus),
                            _ctx.LifetimeToken)
                        .ConfigureAwait(false);
                    _ctx.RaiseServiceMessage(
                        $"Connected to endpoint: {_connectionInfo.ServerEndpoint?.ToString() ?? "unknown"}");
                    _ctx.SetState(XeClientState.Connected);
                }

                var typeSystem = new ComplexTypeSystem(session);
                await typeSystem.LoadAsync().ConfigureAwait(false);

                await WaitAndCheckKeepAliveAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_ctx.LifetimeToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _ctx.SetState(XeClientState.Error);
                _ctx.RaiseServiceException(ex);
            }
            finally
            {
                _ctx.Session = null;
                session?.Dispose();
            }

            await WaitUntilReconnectAsync().ConfigureAwait(false);
        }

        _connect = false;
        _ctx.SetState(XeClientState.Disconnected);
    }

    private async Task WaitAndCheckKeepAliveAsync()
    {
        TimeSpan timeout = _ctx.Options.KeepAliveTimeout;
        _lastKeepAliveTimestamp = Stopwatch.GetTimestamp();

        while (_ctx.State == XeClientState.Connected)
        {
            if (_ctx.LifetimeToken.IsCancellationRequested || !_connect)
            {
                _ctx.SetState(XeClientState.Disconnecting);
                break;
            }

            TimeSpan elapsed = XeClientContext.GetElapsed(_lastKeepAliveTimestamp);

            if (timeout > TimeSpan.Zero && elapsed > timeout)
            {
                _ctx.RaiseServiceMessage("Keep-alive timeout exceeded.");
                _ctx.SetState(XeClientState.Error);
            }
            else
            {
                await _ctx.Session!.ReadNodeAsync(
                        new NodeId(VariableIds.Server_ServerStatus),
                        _ctx.LifetimeToken)
                    .ConfigureAwait(false);
                await Task.Delay(1000, _ctx.LifetimeToken).ConfigureAwait(false);
            }
        }
    }

    private async Task WaitUntilReconnectAsync()
    {
        // Snapshot once: DisconnectAsync may replace _connectionInfo on another thread.
        XeConnectionInfo info = _connectionInfo;
        TimeSpan reconnectDelay = _ctx.Options.AutoReconnectDelay;

        if (reconnectDelay == TimeSpan.Zero || !info.Blocking)
            return;

        if (_ctx.State == XeClientState.Error)
            _ctx.RaiseServiceMessage($"Reconnecting in {reconnectDelay.TotalSeconds:0} seconds…");

        long waitStart = Stopwatch.GetTimestamp();
        while (_ctx.State == XeClientState.Error)
        {
            if (_ctx.LifetimeToken.IsCancellationRequested) return;

            if (XeClientContext.GetElapsed(waitStart) > reconnectDelay)
                _ctx.SetState(XeClientState.Connecting);
            else
                await Task.Delay(100, _ctx.LifetimeToken).ConfigureAwait(false);
        }
    }

    #endregion

    #region Event-driven wait

    /// <summary>
    /// Waits until the client state matches one of <paramref name="targetStates"/>,
    /// or until <paramref name="ct"/> is cancelled.
    /// </summary>
    private async Task WaitForStateAsync(CancellationToken ct, params XeClientState[] targetStates)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnStateChanged(object? _, XeClientStateChangedEventArgs e)
        {
            if (targetStates.Contains(e.State))
                tcs.TrySetResult();
        }

        _ctx.StateChanged += OnStateChanged;

        // Complete immediately if the state already matches (closes the subscription race).
        if (targetStates.Contains(_ctx.State))
            tcs.TrySetResult();

        try
        {
            await tcs.Task.WaitAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _ctx.StateChanged -= OnStateChanged;
        }
    }

    #endregion

    #region Session Creation

    private async Task<ISession> CreateClientSessionAsync()
    {
        XeConnectionInfo info = _connectionInfo;

        // Auto select server endpoint if none was specified
        if (info.ServerEndpoint == null)
        {
            info.ServerEndpoint = await GetAutoServerEndpointInternalAsync(
                info.IpAddress, info.Port, _ctx.LifetimeToken)
                .ConfigureAwait(false);
        }

        XeConnectionEndpoint serverEndpoint = info.ServerEndpoint;
        await UpdateApplicationConfigurationAsync().ConfigureAwait(false);

        EndpointConfiguration endpointConfiguration = EndpointConfiguration.Create(_appConfig);
        ConfiguredEndpoint configuredEndpoint = new(
            null, serverEndpoint.OpcUaEndpointDescription, endpointConfiguration);
        UserIdentity? userIdentity = CreateUserIdentity();

        IList<string> preferredLocales = _ctx.Options.PreferredLocales is not null
            ? _ctx.Options.PreferredLocales.ToList()
            : [CultureInfo.CurrentUICulture.Name];

        var session = await SessionFactory.CreateAsync(
            _appConfig,
            configuredEndpoint,
            updateBeforeConnect: false,
            checkDomain: false,
            $"{_ctx.Options.ClientName}_Session",
            sessionTimeout: 30000,
            identity: userIdentity,
            preferredLocales,
            _ctx.LifetimeToken).ConfigureAwait(false);
        return session;
    }

    private async Task UpdateApplicationConfigurationAsync()
    {
        _appConfig = new ApplicationConfiguration
        {
            ApplicationName = _ctx.Options.ClientName,
            ApplicationUri = Utils.Format(@"urn:{0}:{1}", System.Net.Dns.GetHostName(), _ctx.Options.ClientName),
            ProductUri = "urn:DataLogXE:OpcUaXE.Client",
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = CreateSecurityConfiguration(),
            TransportConfigurations = [],
            TransportQuotas = new TransportQuotas(), // TODO
            ClientConfiguration = new ClientConfiguration(), // TODO
            TraceConfiguration = new TraceConfiguration()
        };

        await _appConfig.ValidateAsync(ApplicationType.Client).ConfigureAwait(false);

        _appConfig.CertificateValidator.CertificateValidation -= CertificateValidation;
        _appConfig.CertificateValidator.CertificateValidation += CertificateValidation;

        ApplicationInstance application = new ApplicationInstance((ITelemetryContext?)null)
        {
            ApplicationName = _ctx.Options.ClientName,
            ApplicationType = ApplicationType.Client,
            ApplicationConfiguration = _appConfig
        };
        await application.CheckApplicationInstanceCertificatesAsync(false, 2048)
            .ConfigureAwait(false);
    }

    private SecurityConfiguration CreateSecurityConfiguration()
    {
        string basePath = ResolveCertificateBasePath();
        string certificatePath = Path.Combine(basePath, "OPC Foundation", "CertificateStores");
        _ctx.RaiseServiceMessage($"Using certificate directory: {certificatePath}");

        return new SecurityConfiguration
        {
            ApplicationCertificate = new CertificateIdentifier
            {
                StoreType = "Directory",
                StorePath = Path.Combine(certificatePath, "MachineDefault"),
                SubjectName = _ctx.Options.ClientName
            },
            TrustedIssuerCertificates = new CertificateTrustList
            {
                StoreType = "Directory",
                StorePath = Path.Combine(certificatePath, "UA Certificate Authorities")
            },
            TrustedPeerCertificates = new CertificateTrustList
            {
                StoreType = "Directory",
                StorePath = Path.Combine(certificatePath, "UA Applications")
            },
            RejectedCertificateStore = new CertificateTrustList
            {
                StoreType = "Directory",
                StorePath = Path.Combine(certificatePath, "RejectedCertificates")
            },
            AutoAcceptUntrustedCertificates = false
        };
    }

    private static string ResolveCertificateBasePath()
    {
        if (OperatingSystem.IsWindows())
            return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        const string appDirName = "OpcUaXE";
        string systemPath = Path.Combine("/var/lib", appDirName);
        if (CanWriteToDirectory(systemPath))
            return systemPath;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appDirName);
    }

    private static bool CanWriteToDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            string testFile = Path.Combine(path, ".write-test");
            File.WriteAllText(testFile, "ok");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private UserIdentity? CreateUserIdentity()
    {
        if (string.IsNullOrEmpty(_ctx.Options.UserName))
            return null;

        byte[] passwordBytes = !string.IsNullOrEmpty(_ctx.Options.Password)
            ? Encoding.UTF8.GetBytes(_ctx.Options.Password)
            : [];
        return new UserIdentity(_ctx.Options.UserName, passwordBytes);
    }

    #endregion

    #region SDK Callbacks

    private void CertificateValidation(CertificateValidator sender, CertificateValidationEventArgs e)
    {
        XeCertificateValidationEventArgs eventArgs = new(e.Certificate);
        _ctx.RaiseServiceMessage($"Certificate validation requested [{eventArgs.Certificate.Issuer}]");
        CertificateValidationRequested?.Invoke(this, eventArgs);

        e.Accept = eventArgs.Accept || eventArgs.AcceptPermanently;
        _ctx.RaiseServiceMessage(e.Accept ? "Certificate accepted." : "Certificate rejected.");

        if (eventArgs.AcceptPermanently)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using ICertificateStore store =
                        _appConfig.SecurityConfiguration.TrustedPeerCertificates.OpenStore(null);
                    await store.AddAsync(e.Certificate).ConfigureAwait(false);
                    _ctx.RaiseServiceMessage("Certificate added to trusted peer store.");
                }
                catch (Exception ex)
                {
                    _ctx.RaiseServiceException(ex);
                }
            }, _ctx.LifetimeToken);
        }
    }

    private void SessionKeepAlive(ISession session, KeepAliveEventArgs e)
    {
        if (e.CurrentState == ServerState.Running)
            _lastKeepAliveTimestamp = Stopwatch.GetTimestamp();

        KeepAliveReceived?.Invoke(this, new XeKeepAliveEventArgs(e.CurrentTime));
    }

    #endregion

    #region Endpoint Discovery

    private async Task<List<XeConnectionEndpoint>> BrowseServerEndpointsInternalAsync(
        string ipAddress, int port, CancellationToken ct)
    {
        List<XeConnectionEndpoint> result = [];
        string url = $"opc.tcp://{ipAddress}:{port}";
        _ctx.RaiseServiceMessage($"Requesting server endpoints from [{url}]");

        try
        {
            Uri uri = new(url);
            using DiscoveryClient client = await DiscoveryClient.CreateAsync(uri, null)
                .ConfigureAwait(false);
            EndpointDescriptionCollection endpoints =
                await client.GetEndpointsAsync(null).ConfigureAwait(false);

            foreach (EndpointDescription endpoint in endpoints)
                result.Add(new XeConnectionEndpoint(endpoint));

            _ctx.RaiseServiceMessage($"Found {result.Count} endpoint(s).");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _ctx.RaiseServiceException(ex);
        }

        return result;
    }

    private async Task<XeConnectionEndpoint> GetAutoServerEndpointInternalAsync(
        string ipAddress, int port, CancellationToken ct)
    {
        List<XeConnectionEndpoint> endpoints =
            await BrowseServerEndpointsInternalAsync(ipAddress, port, ct).ConfigureAwait(false);

        if (endpoints.Count == 0)
            throw new XeConnectionException($"No OPC UA endpoint found on {ipAddress}:{port}.");

        XeConnectionEndpoint? none = endpoints.FirstOrDefault(
            ep => ep.OpcUaEndpointDescription.SecurityMode == MessageSecurityMode.None);
        if (none != null)
            return none;

        return endpoints.OrderByDescending(ep => ep.SecurityLevel).First();
    }

    #endregion
}
