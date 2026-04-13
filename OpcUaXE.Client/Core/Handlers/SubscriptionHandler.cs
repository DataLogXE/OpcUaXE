using Opc.Ua;
using Opc.Ua.Client;
using OpcUaXE.Client.Core.Helper;
using OpcUaXE.Client.Exceptions;
using OpcUaXE.Client.Types;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace OpcUaXE.Client.Core.Handlers;

/// <summary>
/// Manages OPC UA monitored items and subscriptions, keeping the client-side item list
/// in sync with the active session and raising <see cref="MonitoredItemValuesReceived"/> on changes.
/// </summary>
internal sealed class SubscriptionHandler : ISubscriptionHandler
{
    private readonly XeClientContext _ctx;

    public event EventHandler<IReadOnlyList<XeReadItem>>? MonitoredItemValuesReceived;

    // Delay (in 1-second ticks) after connect before first subscription sync.
    private const int SyncStartDelay = 3;
    private const int CleanupInterval = 10;
    private const int ChannelCapacity = 100_000;
    private const string ChannelName = "MonitoredItemValues";

    private readonly List<XeMonitoredItem> _monitoredItemsList = [];

    // Written exclusively from SyncLoopAsync; read concurrently from SDK callbacks.
    private readonly ConcurrentDictionary<string, XeMonitoredItem> _monitoredItemsDict =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Bounded channel decoupling the SDK callback thread from the consumer task.
    /// <see cref="BoundedChannelFullMode.DropWrite"/> activates overflow self-protection.
    /// </summary>
    private readonly Channel<XeReadItem> _monitoredItemValues = Channel.CreateBounded<XeReadItem>(
        new BoundedChannelOptions(ChannelCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite
        });

    private volatile bool _channelOverflowActive;
    private int _connectedCounter;
    private int _cleanupCounter;

    public SubscriptionHandler(XeClientContext ctx) => _ctx = ctx;

    #region Public API

    public void AddMonitoredItem(string nodeAddress, int samplingIntervalMs, int publishIntervalMs)
    {
        AddMonitoredItems([new XeMonitoredItem(nodeAddress, samplingIntervalMs, publishIntervalMs)]);
    }

    public void AddMonitoredItems(IReadOnlyList<XeMonitoredItem> items)
    {
        lock (_monitoredItemsList)
        {
            HashSet<string> existing = _monitoredItemsList
                .Select(i => i.Address.NodeAddress)
                .ToHashSet(StringComparer.Ordinal);

            foreach (XeMonitoredItem item in items)
            {
                if (existing.Add(item.Address.NodeAddress))
                    _monitoredItemsList.Add(item);
            }
        }
    }

    public void RemoveMonitoredItem(string nodeAddress) =>
        RemoveMonitoredItems([nodeAddress]);

    public void RemoveMonitoredItems(IReadOnlyList<string> nodeAddresses)
    {
        lock (_monitoredItemsList)
        {
            foreach (string addr in nodeAddresses)
            {
                XeMonitoredItem? item = _monitoredItemsList
                    .FirstOrDefault(mi => mi.Address.NodeAddress == addr);
                if (item != null)
                    _monitoredItemsList.Remove(item);
            }
        }
    }

    public void RemoveAllMonitoredItems()
    {
        lock (_monitoredItemsList)
        {
            _monitoredItemsList.Clear();
        }
    }

    #endregion

    #region Background Loops

    public async Task SyncLoopAsync()
    {
        while (!_ctx.LifetimeToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, _ctx.LifetimeToken).ConfigureAwait(false);

                _connectedCounter = _ctx.IsConnected
                    ? Math.Clamp(++_connectedCounter, 0, 100)
                    : 0;

                if (_connectedCounter >= SyncStartDelay)
                {
                    ResolveNodeIds();
                    UpdateDictionary();
                    await SyncWithOpcUaSessionAsync().ConfigureAwait(false);
                }
                else
                {
                    _cleanupCounter = 0;
                }
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

    public async Task ConsumerLoopAsync()
    {
        await foreach (XeReadItem first in
            _monitoredItemValues.Reader.ReadAllAsync(_ctx.LifetimeToken).ConfigureAwait(false))
        {
            _channelOverflowActive = false;
            List<XeReadItem> batch = [first];
            while (_monitoredItemValues.Reader.TryRead(out XeReadItem? more))
                batch.Add(more);

            try
            {
                MonitoredItemValuesReceived?.Invoke(this, batch);
            }
            catch (Exception ex)
            {
                _ctx.RaiseServiceException(ex);
            }
        }
    }

    #endregion

    #region Sync: Reconcile Client List with OPC UA Session

    private void ResolveNodeIds()
    {
        List<XeMonitoredItem> unresolved;
        lock (_monitoredItemsList)
        {
            unresolved = _monitoredItemsList.Where(mi => !mi.Address.IsValid).ToList();
        }

        foreach (XeMonitoredItem item in unresolved)
            item.Address.CheckValid(_ctx.Session!);
    }

    private void UpdateDictionary()
    {
        List<XeMonitoredItem> valid;
        lock (_monitoredItemsList)
        {
            valid = _monitoredItemsList.Where(mi => mi.Address.IsValid).ToList();
        }

        IEnumerable<string> toAdd = valid
            .Select(mi => mi.Address.NodeIdString)
            .Except(_monitoredItemsDict.Keys, StringComparer.Ordinal);

        IEnumerable<string> toRemove = _monitoredItemsDict.Keys
            .Except(valid.Select(mi => mi.Address.NodeIdString), StringComparer.Ordinal)
            .ToList();

        foreach (string nodeId in toAdd)
        {
            XeMonitoredItem item = valid.First(mi => mi.Address.NodeIdString == nodeId);
            _monitoredItemsDict.TryAdd(nodeId, item);
        }

        foreach (string nodeId in toRemove)
            _monitoredItemsDict.TryRemove(nodeId, out _);
    }

    private async Task SyncWithOpcUaSessionAsync()
    {
        List<XeSessionMonitoredItem> sessionItems = GetAllSessionMonitoredItems();

        IEnumerable<string> clientNodeIds = _monitoredItemsDict.Keys;

        IEnumerable<string> idsToAdd =
            clientNodeIds.Except(sessionItems.Select(s => s.NodeId)).ToList();
        IEnumerable<XeMonitoredItem> itemsToAdd = idsToAdd.Select(id => _monitoredItemsDict[id]);

        IEnumerable<string> idsToRemove =
            sessionItems.Select(s => s.NodeId).Except(clientNodeIds).ToList();
        IEnumerable<XeSessionMonitoredItem> itemsToRemove =
            sessionItems.Where(s => idsToRemove.Contains(s.NodeId));

        await RemoveSessionMonitoredItemsAsync(itemsToRemove).ConfigureAwait(false);
        await AddMonitoredItemsToSessionAsync(itemsToAdd).ConfigureAwait(false);
        await CleanupInvalidSessionItemsAsync(sessionItems).ConfigureAwait(false);
    }

    private async Task CleanupInvalidSessionItemsAsync(List<XeSessionMonitoredItem> sessionItems)
    {
        if (++_cleanupCounter % CleanupInterval != 0) return;
        _cleanupCounter = 0;

        List<XeSessionMonitoredItem> invalid =
            sessionItems.Where(i => !i.MonitoredItem.Created).ToList();
        if (invalid.Count == 0) return;

        List<XeSessionMonitoredItem> removable = [];

        foreach (XeSessionMonitoredItem item in invalid)
        {
            try
            {
                await _ctx.Session!.ReadNodeAsync(
                    item.MonitoredItem.StartNodeId, _ctx.LifetimeToken).ConfigureAwait(false);
                removable.Add(item);
            }
            catch
            {
                // Node not yet readable in this session – skip cleanup this cycle.
            }
        }

        if (removable.Count > 0)
            await RemoveSessionMonitoredItemsAsync(removable).ConfigureAwait(false);
    }

    private async Task AddMonitoredItemsToSessionAsync(IEnumerable<XeMonitoredItem> itemsToAdd)
    {
        List<XeMonitoredItem> list = itemsToAdd.ToList();
        if (list.Count == 0) return;

        foreach (IGrouping<int, XeMonitoredItem> group in list.GroupBy(i => i.PublishIntervalMs))
        {
            Subscription? subscription =
                await GetOrCreateSubscriptionAsync(group.Key).ConfigureAwait(false);
            if (subscription == null) continue;

            try
            {
                List<MonitoredItem> toAdd = [];
                foreach (XeMonitoredItem item in group)
                {
                    MonitoredItem mi = new(subscription.DefaultItem)
                    {
                        StartNodeId = item.Address.NodeId!,
                        SamplingInterval = item.SamplingIntervalMs,
                        QueueSize = uint.MaxValue,
                        DiscardOldest = true
                    };
                    mi.Notification += OnMonitoredItemNotification;
                    toAdd.Add(mi);
                }
                subscription.AddItems(toAdd);
                await subscription.ApplyChangesAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _ctx.RaiseServiceException(ex);
            }
        }

        _ctx.RaiseServiceMessage($"Added {list.Count} monitored item(s).");
    }

    private async Task<Subscription?> GetOrCreateSubscriptionAsync(int publishInterval)
    {
        string name = $"Subscription_{publishInterval}_ms";
        Subscription? sub = _ctx.Session!.Subscriptions.FirstOrDefault(s => s.DisplayName == name);
        if (sub != null) return sub;

        try
        {
            sub = new Subscription(_ctx.Session.DefaultSubscription)
            {
                PublishingInterval = publishInterval,
                DisplayName = name
            };
            _ctx.Session.AddSubscription(sub);
            await sub.CreateAsync().ConfigureAwait(false);
            _ctx.RaiseServiceMessage($"Created subscription '{name}'.");
        }
        catch (Exception ex)
        {
            _ctx.RaiseServiceException(ex);
            return null;
        }
        return sub;
    }

    private async Task RemoveSessionMonitoredItemsAsync(
        IEnumerable<XeSessionMonitoredItem> itemsToRemove)
    {
        List<XeSessionMonitoredItem> list = itemsToRemove.ToList();
        if (list.Count == 0) return;

        foreach (IGrouping<Subscription?, XeSessionMonitoredItem> group in
                 list.GroupBy(i => i.MonitoredItem.Subscription))
        {
            Subscription? sub = group.Key;
            if (sub == null) continue;

            try
            {
                foreach (XeSessionMonitoredItem item in group)
                    item.MonitoredItem.Notification -= OnMonitoredItemNotification;

                sub.RemoveItems(group.Select(i => i.MonitoredItem));
                await sub.ApplyChangesAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _ctx.RaiseServiceException(ex);
                continue;
            }

            try
            {
                if (!sub.MonitoredItems.Any())
                {
                    _ctx.RaiseServiceMessage($"Deleting subscription '{sub.DisplayName}'.");
                    await _ctx.Session!.RemoveSubscriptionAsync(sub).ConfigureAwait(false);
                    await sub.DeleteAsync(true).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _ctx.RaiseServiceException(ex);
            }
        }

        _ctx.RaiseServiceMessage($"Removed {list.Count} monitored item(s).");
    }

    private List<XeSessionMonitoredItem> GetAllSessionMonitoredItems()
    {
        List<XeSessionMonitoredItem> result = [];
        foreach (Subscription sub in _ctx.Session!.Subscriptions)
        {
            if (sub.DisplayName.StartsWith(XeClientContext.AcSubscriptionName,
                    StringComparison.Ordinal))
                continue;
            foreach (MonitoredItem mi in sub.MonitoredItems)
                result.Add(new XeSessionMonitoredItem(mi));
        }
        return result;
    }

    #endregion

    #region SDK Callback

    private void OnMonitoredItemNotification(
        MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
    {
        if (e.NotificationValue is not MonitoredItemNotification notification
            || notification.Value.Value is null)
            return;

        string nodeId = monitoredItem.StartNodeId.ToString();
        if (!_monitoredItemsDict.TryGetValue(nodeId, out XeMonitoredItem? item))
            return;

        XeReadItem xei = new(item.Address, notification.Value);

        if (_monitoredItemValues.Writer.TryWrite(xei))
            return;

        if (_channelOverflowActive)
            return;

        _channelOverflowActive = true;
        int drained = 0;
        while (_monitoredItemValues.Reader.TryRead(out _))
            drained++;

        _ctx.RaiseServiceException(new XeChannelOverflowException(ChannelName, drained, ChannelCapacity));
        _monitoredItemValues.Writer.TryWrite(xei);
    }

    #endregion
}
