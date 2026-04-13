using System.Runtime.Serialization;

namespace OpcUaXE.Client.Exceptions;

/// <summary>Thrown when an OPC UA browse operation fails.</summary>
[Serializable]
public class XeBrowseException : Exception
{
    /// <summary>Initializes with an error message.</summary>
    /// <param name="message">Error description.</param>
    public XeBrowseException(string message) : base(message) { }

    /// <summary>Initializes with an error message and inner exception.</summary>
    /// <param name="message">Error description.</param>
    /// <param name="inner">Causing exception.</param>
    public XeBrowseException(string message, Exception inner) : base(message, inner) { }

#pragma warning disable SYSLIB0051
    /// <inheritdoc/>
    protected XeBrowseException(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
#pragma warning restore SYSLIB0051
}
