using System.Runtime.Serialization;

namespace OpcUaXE.Client.Exceptions;

/// <summary>
/// Raised when an internal notification channel overflows and buffered events are discarded.
/// The channel is drained automatically; processing resumes afterwards.
/// </summary>
[Serializable]
public sealed class XeChannelOverflowException : Exception
{
    /// <summary>Number of buffered events that were discarded when the overflow was detected.</summary>
    public int DiscardedCount { get; }

    /// <summary>Maximum number of items the channel can buffer before overflow protection activates.</summary>
    public int ChannelCapacity { get; }

    /// <summary>Name of the channel that overflowed (for diagnostics).</summary>
    public string ChannelName { get; }

    /// <summary>Initializes with overflow details.</summary>
    /// <param name="channelName">Name of the channel that overflowed.</param>
    /// <param name="discardedCount">Number of discarded events.</param>
    /// <param name="channelCapacity">Configured channel capacity.</param>
    public XeChannelOverflowException(string channelName, int discardedCount, int channelCapacity)
        : base($"Channel '{channelName}' overflow: {discardedCount} buffered events discarded. " +
               $"The event handler is too slow (capacity: {channelCapacity}).")
    {
        ChannelName = channelName;
        DiscardedCount = discardedCount;
        ChannelCapacity = channelCapacity;
    }

#pragma warning disable SYSLIB0051
    // Private (not protected) because the class is sealed – CA2229 allows private for sealed types.
    private XeChannelOverflowException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        ChannelName = info.GetString(nameof(ChannelName)) ?? string.Empty;
        DiscardedCount = info.GetInt32(nameof(DiscardedCount));
        ChannelCapacity = info.GetInt32(nameof(ChannelCapacity));
    }

    /// <inheritdoc/>
    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(ChannelName), ChannelName);
        info.AddValue(nameof(DiscardedCount), DiscardedCount);
        info.AddValue(nameof(ChannelCapacity), ChannelCapacity);
    }
#pragma warning restore SYSLIB0051
}
