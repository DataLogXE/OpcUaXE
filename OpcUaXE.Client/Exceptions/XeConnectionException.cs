using System.Runtime.Serialization;

namespace OpcUaXE.Client.Exceptions;

/// <summary>Thrown when a connection to an OPC UA server fails.</summary>
[Serializable]
public class XeConnectionException : Exception
{
    /// <summary>Initializes with an error message.</summary>
    /// <param name="message">Error description.</param>
    public XeConnectionException(string message) : base(message) { }

    /// <summary>Initializes with an error message and inner exception.</summary>
    /// <param name="message">Error description.</param>
    /// <param name="inner">Causing exception.</param>
    public XeConnectionException(string message, Exception inner) : base(message, inner) { }

#pragma warning disable SYSLIB0051
    /// <inheritdoc/>
    protected XeConnectionException(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
#pragma warning restore SYSLIB0051
}
