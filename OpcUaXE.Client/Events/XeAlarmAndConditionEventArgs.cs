namespace OpcUaXE.Client.Events;

/// <summary>
/// Event data for OPC UA Alarm &amp; Condition notifications.
/// All OPC UA SDK types are mapped to BCL types to avoid leaking the underlying stack.
/// </summary>
public sealed class XeAlarmAndConditionEventArgs : EventArgs
{
    /// <summary>
    /// Unique identifier for the event instance (server-generated).
    /// Encoded as a hex string (OPC UA <c>ByteString</c>); <see langword="null"/> if not present.
    /// </summary>
    public string? EventId { get; init; }

    /// <summary>
    /// String representation of the event type <c>NodeId</c>
    /// (e.g. subtype of <c>BaseEventType</c>); <see langword="null"/> if not resolved.
    /// </summary>
    public string? EventTypeNodeId { get; init; }

    /// <summary>
    /// Display name of the event type node, resolved from the server on first occurrence and cached.
    /// <see langword="null"/> if the type could not be resolved.
    /// </summary>
    public string? EventTypeDisplayName { get; internal set; }

    /// <summary>Human-readable name of the event source.</summary>
    public string? SourceName { get; init; }

    /// <summary>Time the event occurred at the source (<c>BaseEventType.Time</c>, UTC).</summary>
    public DateTime TimeUtc { get; init; }

    /// <summary>Time the server received the event (<c>BaseEventType.ReceiveTime</c>, UTC).</summary>
    public DateTime ReceiveTimeUtc { get; init; }

    /// <summary>Human-readable name of the condition (<c>ConditionType.ConditionName</c>).</summary>
    public string? ConditionName { get; init; }

    /// <summary>
    /// Alarm active state (<c>AlarmConditionType.ActiveState/Id</c>; TwoState boolean id).
    /// </summary>
    public bool? ActiveState { get; init; }

    /// <summary>
    /// Acknowledged state (<c>AcknowledgeableConditionType.AckedState/Id</c>; TwoState boolean id).
    /// </summary>
    public bool? AckedState { get; init; }

    /// <summary>
    /// Indicates whether the condition should be retained in the server's event history
    /// (<c>BaseEventType.Retain</c>).
    /// </summary>
    public bool? Retain { get; init; }

    /// <summary>Event message text from the server (<c>BaseEventType.Message</c>).</summary>
    public string? Message { get; init; }

    /// <summary>Severity (0 = lowest; range and meaning are defined by the server).</summary>
    public ushort Severity { get; init; }

    /// <inheritdoc />
    public override string ToString() =>
        $"TimeUtc={TimeUtc:yyyy-MM-ddTHH:mm:ss.ffffffZ}; "
        + $"EventType={EventTypeDisplayName ?? EventTypeNodeId}; "
        + $"ConditionName={ConditionName}; "
        + $"Retain={Retain,-5}; "
        + $"ActiveState={ActiveState,-5}; "
        + $"AckedState={AckedState,-5}; "
        + $"Severity={Severity,3}; "
        + $"Message={Message}; ";
}
