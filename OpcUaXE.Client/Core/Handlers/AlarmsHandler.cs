using Opc.Ua;
using Opc.Ua.Client;
using OpcUaXE.Client.Events;
using OpcUaXE.Client.Exceptions;
using OpcUaXE.Client.Types;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace OpcUaXE.Client.Core.Handlers;

/// <summary>
/// Handles OPC UA Alarms and Conditions by registering an event-notifier subscription
/// when <see cref="XeClientOptions.AlarmsAndConditionsEnable"/> is <see langword="true"/>.
/// </summary>
internal sealed class AlarmsHandler : IAlarmsHandler
{
    private readonly XeClientContext _ctx;

    public event EventHandler<XeAlarmAndConditionEventArgs>? AlarmAndConditionEventReceived;

    private const int ChannelCapacity = 100_000;
    private const string ChannelName = "AlarmAndConditionNotifications";

    private enum AcState { Init, Cleanup, Register, Running, Error }

    private Subscription? _subscription;
    private MonitoredItem? _monitoredItem;
    private AcState _acState = AcState.Init;
    private AcState _lastAcState = AcState.Init;
    private int _tickCount;
    private DateTime _nextAutoRefreshUtc = DateTime.MaxValue;

    private readonly ConcurrentDictionary<string, string> _eventTypeNameCache =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Single long-lived channel; drained (not recreated) on reconnect to avoid
    /// spawning untracked consumer tasks.
    /// </summary>
    private readonly Channel<EventFieldList> _notificationChannel =
        Channel.CreateBounded<EventFieldList>(
            new BoundedChannelOptions(ChannelCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropWrite
            });

    private volatile bool _channelOverflowActive;

    public AlarmsHandler(XeClientContext ctx) => _ctx = ctx;

    #region Background Loops

    public async Task AlarmsLoopAsync()
    {
        while (!_ctx.LifetimeToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, _ctx.LifetimeToken).ConfigureAwait(false);
                await TickAsync().ConfigureAwait(false);
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

    public async Task NotificationLoopAsync()
    {
        await foreach (EventFieldList fieldList in
            _notificationChannel.Reader.ReadAllAsync(_ctx.LifetimeToken).ConfigureAwait(false))
        {
            _channelOverflowActive = false;
            try
            {
                XeAlarmAndConditionEventArgs args = MapFromEventFields(fieldList);
                args.EventTypeDisplayName =
                    await ResolveEventTypeNameAsync(args.EventTypeNodeId).ConfigureAwait(false);
                AlarmAndConditionEventReceived?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _ctx.RaiseServiceException(ex);
            }
        }
    }

    #endregion

    #region State Machine

    private async Task TickAsync()
    {
        switch (_acState)
        {
            case AcState.Init: await StateInitAsync().ConfigureAwait(false); break;
            case AcState.Cleanup: await StateCleanupAsync().ConfigureAwait(false); break;
            case AcState.Register: await StateRegisterAsync().ConfigureAwait(false); break;
            case AcState.Running: StateRunning(); break;
            case AcState.Error: await StateErrorAsync().ConfigureAwait(false); break;
        }

        if (_acState != _lastAcState)
        {
            _ctx.RaiseServiceMessage($"Alarms and conditions subscription: {_lastAcState} > {_acState}");
            _lastAcState = _acState;
            _tickCount = 0;
        }
    }

    private async Task StateInitAsync()
    {
        if (_ctx.IsConnected && _ctx.Options.AlarmsAndConditionsEnable)
        {
            await Task.Delay(1000, _ctx.LifetimeToken).ConfigureAwait(false);
            _acState = AcState.Cleanup;
        }
    }

    private async Task StateCleanupAsync()
    {
        _channelOverflowActive = false;
        while (_notificationChannel.Reader.TryRead(out _)) { }

        try
        {
            _monitoredItem = null;
            _subscription?.Dispose();
            _subscription = null;
        }
        catch (Exception ex)
        {
            _ctx.RaiseServiceException(ex);
        }

        if (_ctx.IsConnected)
        {
            await Task.Delay(1000, _ctx.LifetimeToken).ConfigureAwait(false);
            _acState = AcState.Register;
        }
        else
        {
            _acState = AcState.Init;
        }
    }

    private async Task StateRegisterAsync()
    {
        try
        {
            string nodeAddress = _ctx.Options.AlarmsAndConditionsNodeId.Trim();
            XeNodeAddress address = new(nodeAddress, _ctx.Session!);
            address.CheckValid(_ctx.Session!);

            if (!address.IsValid || address.NodeId == null)
            {
                _ctx.RaiseServiceMessage($"Invalid A&C node address: '{nodeAddress}'");
                _acState = AcState.Error;
                return;
            }

            await RegisterNotifierNodeAsync(address.NodeId).ConfigureAwait(false);
            _ctx.RaiseServiceMessage($"A&C event notifier registered for node '{address.NodeId}'.");
            ScheduleNextAutoRefresh();
            _acState = AcState.Running;
        }
        catch (Exception ex)
        {
            _acState = AcState.Error;
            _ctx.RaiseServiceMessage("Failed to register A&C event notifier.");
            _ctx.RaiseServiceException(ex);
        }
    }

    private void StateRunning()
    {
        if (!_ctx.IsConnected)
        {
            _acState = AcState.Cleanup;
            return;
        }

        if (_monitoredItem == null || !_monitoredItem.Created)
        {
            if (++_tickCount >= 10)
            {
                _ctx.RaiseServiceMessage(
                    "A&C monitored item was not created within the expected time.");
                _acState = AcState.Cleanup;
            }
            return;
        }

        if (DateTime.UtcNow >= _nextAutoRefreshUtc)
        {
            ScheduleNextAutoRefresh();
            _ = CallConditionRefreshAsync(_ctx.LifetimeToken);
        }
    }

    private void ScheduleNextAutoRefresh()
    {
        TimeSpan interval = _ctx.Options.AlarmConditionRefreshInterval;
        _nextAutoRefreshUtc = interval > TimeSpan.Zero
            ? DateTime.UtcNow.Add(interval)
            : DateTime.MaxValue;
    }

    private async Task StateErrorAsync()
    {
        if (!_ctx.IsConnected)
            _acState = AcState.Cleanup;

        await Task.Delay(1000, _ctx.LifetimeToken).ConfigureAwait(false);
    }

    #endregion

    #region OPC UA Registration

    private async Task RegisterNotifierNodeAsync(NodeId nodeId)
    {
        Subscription? stale = _ctx.Session!.Subscriptions
            .FirstOrDefault(s => s.DisplayName == XeClientContext.AcSubscriptionName);
        if (stale != null)
        {
            try { await stale.DeleteAsync(true).ConfigureAwait(false); }
            catch (Exception ex) { _ctx.RaiseServiceException(ex); }
            await Task.Delay(1000, _ctx.LifetimeToken).ConfigureAwait(false);
        }

        _subscription = new Subscription(_ctx.Session!.DefaultSubscription)
        {
            PublishingInterval = 1000,
            DisplayName = XeClientContext.AcSubscriptionName
        };
        _ctx.Session!.AddSubscription(_subscription);
        await _subscription.CreateAsync().ConfigureAwait(false);

        _monitoredItem = new MonitoredItem(_subscription.DefaultItem)
        {
            StartNodeId = nodeId,
            NodeClass = NodeClass.Object,
            AttributeId = Attributes.EventNotifier,
            MonitoringMode = MonitoringMode.Reporting,
            SamplingInterval = 0,
            QueueSize = uint.MaxValue,
            DiscardOldest = true,
            Filter = BuildEventFilter()
        };
        _monitoredItem.Notification -= OnAcNotification;
        _monitoredItem.Notification += OnAcNotification;
        _subscription.AddItem(_monitoredItem);
        await _subscription.ApplyChangesAsync().ConfigureAwait(false);

        await CallConditionRefreshAsync(_ctx.LifetimeToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RequestConditionRefreshAsync(CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _ctx.LifetimeToken);
        await CallConditionRefreshAsync(linked.Token).ConfigureAwait(false);
    }

    private async Task CallConditionRefreshAsync(CancellationToken ct)
    {
        if (_subscription == null || !_subscription.Created)
            return;

        try
        {
            await _ctx.Session!.CallAsync(
                requestHeader: null,
                methodsToCall:
                [
                    new CallMethodRequest
                    {
                        ObjectId = ObjectTypeIds.ConditionType,
                        MethodId = MethodIds.ConditionType_ConditionRefresh,
                        InputArguments = [new Variant(_subscription.Id)]
                    }
                ],
                ct: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _ctx.RaiseServiceMessage("ConditionRefresh failed (server may not support it).");
            _ctx.RaiseServiceException(ex);
        }
    }

    #endregion

    #region Event Filter

    /// <summary>
    /// Builds the event filter with select clauses [0]–[10].
    /// Field order must match <see cref="MapFromEventFields"/> indices.
    /// </summary>
    internal static EventFilter BuildEventFilter()
    {
        EventFilter filter = new() { WhereClause = new ContentFilter() };

        void Add(NodeId typeId, string browseName) =>
            filter.AddSelectClause(typeId, new QualifiedName(browseName, 0));

        Add(ObjectTypes.BaseEventType, BrowseNames.EventId);        // [0]
        Add(ObjectTypes.BaseEventType, BrowseNames.EventType);      // [1]
        Add(ObjectTypes.BaseEventType, BrowseNames.SourceName);     // [2]
        Add(ObjectTypes.BaseEventType, BrowseNames.Time);           // [3]
        Add(ObjectTypes.BaseEventType, BrowseNames.ReceiveTime);    // [4]
        Add(ObjectTypes.BaseEventType, BrowseNames.Message);        // [5]
        Add(ObjectTypes.BaseEventType, BrowseNames.Severity);       // [6]
        Add(ObjectTypes.ConditionType, BrowseNames.ConditionName);  // [7]
        Add(ObjectTypes.BaseEventType, BrowseNames.Retain);         // [8]

        // [9] ActiveState/Id
        filter.SelectClauses.Add(new SimpleAttributeOperand(
            ObjectTypes.AlarmConditionType,
            [new QualifiedName(BrowseNames.ActiveState, 0), new QualifiedName("Id", 0)]));

        // [10] AckedState/Id
        filter.SelectClauses.Add(new SimpleAttributeOperand(
            ObjectTypes.AcknowledgeableConditionType,
            [new QualifiedName(BrowseNames.AckedState, 0), new QualifiedName("Id", 0)]));

        return filter;
    }

    #endregion

    /// <summary>
    /// Enqueues a synthetic <see cref="EventFieldList"/> for unit tests (same channel as live OPC UA notifications).
    /// </summary>
    internal bool TryEnqueueTestNotification(EventFieldList fieldList) =>
        _notificationChannel.Writer.TryWrite(fieldList);

    #region SDK Callback

    private void OnAcNotification(
        MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
    {
        if (e.NotificationValue is not EventFieldList fieldList
            || fieldList.EventFields?.Count == 0)
            return;

        if (_notificationChannel.Writer.TryWrite(fieldList))
            return;

        if (_channelOverflowActive)
            return;

        _channelOverflowActive = true;
        int drained = 0;
        while (_notificationChannel.Reader.TryRead(out _))
            drained++;

        _ctx.RaiseServiceException(
            new XeChannelOverflowException(ChannelName, drained, ChannelCapacity));
        _notificationChannel.Writer.TryWrite(fieldList);
    }

    #endregion

    #region Event Field Mapping

    internal XeAlarmAndConditionEventArgs MapFromEventFields(EventFieldList fieldList)
    {
        VariantCollection f = fieldList.EventFields;
        Variant Get(int i) => i < f.Count ? f[i] : default;

        byte[]? eventId = Unwrap(Get(0)) as byte[];
        string? eventIdHex = eventId != null ? Convert.ToHexString(eventId) : null;
        NodeId? eventTypeNodeId = ToNodeId(Get(1));

        return new XeAlarmAndConditionEventArgs
        {
            EventId = eventIdHex,
            EventTypeNodeId = eventTypeNodeId?.ToString(),
            SourceName = Unwrap(Get(2)) as string,
            TimeUtc = Unwrap(Get(3)) is DateTime dt ? dt : default,
            ReceiveTimeUtc = Unwrap(Get(4)) is DateTime rt ? rt : default,
            Message = ToLocalizedTextString(Get(5)),
            Severity = ToUInt16(Get(6)),
            ConditionName = Unwrap(Get(7)) as string,
            Retain = ToNullableBoolean(Get(8)),
            ActiveState = ToNullableBoolean(Get(9)),
            AckedState = ToNullableBoolean(Get(10))
        };
    }

    private async Task<string?> ResolveEventTypeNameAsync(string? eventTypeNodeId)
    {
        if (eventTypeNodeId == null) return null;

        if (_eventTypeNameCache.TryGetValue(eventTypeNodeId, out string? cached))
            return cached;

        try
        {
            NodeId nodeId = NodeId.Parse(eventTypeNodeId);
            ReadResponse response = await _ctx.Session!.ReadAsync(
                requestHeader: null,
                maxAge: 0,
                timestampsToReturn: TimestampsToReturn.Neither,
                nodesToRead: [new ReadValueId { NodeId = nodeId, AttributeId = Attributes.DisplayName }],
                ct: _ctx.LifetimeToken).ConfigureAwait(false);

            if (response?.Results?.Count > 0
                && response.Results[0].Value is LocalizedText lt
                && !string.IsNullOrEmpty(lt.Text))
            {
                _eventTypeNameCache[eventTypeNodeId] = lt.Text;
                return lt.Text;
            }
        }
        catch (Exception ex)
        {
            _ctx.RaiseServiceException(ex);
        }

        return null;
    }

    #endregion

    #region Variant Helpers

    private static object Unwrap(Variant v)
    {
        object val = v.Value;
        while (val is ExtensionObject ext && ext.Body != null)
            val = ext.Body;
        return val;
    }

    private NodeId? ToNodeId(Variant v)
    {
        if (v.TypeInfo == null) return null;
        return Unwrap(v) switch
        {
            NodeId n => n,
            ExpandedNodeId e => ExpandedNodeId.ToNodeId(e, _ctx.Session?.NamespaceUris),
            _ => null
        };
    }

    private static string? ToLocalizedTextString(Variant v)
    {
        if (v.TypeInfo == null) return null;
        return Unwrap(v) switch
        {
            LocalizedText lt => lt.Text,
            string s => s,
            _ => null
        };
    }

    private static ushort ToUInt16(Variant v)
    {
        if (v.TypeInfo == null) return 0;
        return Unwrap(v) switch
        {
            ushort u => u,
            IConvertible c when c is not string => SafeToUInt16(c),
            _ => 0
        };
    }

    private static ushort SafeToUInt16(IConvertible c)
    {
        try { return (ushort)Math.Clamp(c.ToInt64(null), 0, ushort.MaxValue); }
        catch { return 0; }
    }

    private static bool? ToNullableBoolean(Variant v)
    {
        if (v.TypeInfo == null || v.TypeInfo.BuiltInType == BuiltInType.Null) return null;
        return Unwrap(v) switch
        {
            bool b => b,
            LocalizedText lt => TryParseBool(lt.Text),
            string s => TryParseBool(s),
            _ => null
        };
    }

    private static bool? TryParseBool(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (bool.TryParse(text, out bool b)) return b;
        if (int.TryParse(text, out int i)) return i != 0;
        // OPC UA TwoStateVariable Id often uses LocalizedText "Active"/"Inactive" (Part 9).
        if (text.Equals("Active", StringComparison.OrdinalIgnoreCase)) return true;
        if (text.Equals("Inactive", StringComparison.OrdinalIgnoreCase)) return false;
        return null;
    }

    #endregion
}
