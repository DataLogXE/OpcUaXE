using Opc.Ua;

namespace OpcUaXE.Client.Types;

/// <summary>Describes an OPC UA server endpoint returned by endpoint discovery.</summary>
public sealed class XeConnectionEndpoint
{
    internal XeConnectionEndpoint(EndpointDescription opcUaEndpointDescription)
    {
        OpcUaEndpointDescription = opcUaEndpointDescription;
    }

    #region internal properties
    /// <summary>Underlying OPC UA SDK endpoint description.</summary>
    internal EndpointDescription OpcUaEndpointDescription { get; private set; }
    #endregion

    #region public properties
    /// <summary>
    /// Gets the URL of the endpoint.
    /// </summary>
    public string EndpointUrl { get => OpcUaEndpointDescription.EndpointUrl; }

    /// <summary>
    /// Gets the security level of the endpoint as reported by the OPC UA server.
    /// A higher value indicates a more secure configuration.
    /// </summary>
    public int SecurityLevel { get => GetSecurityLevel(); }
    #endregion

    #region public methods
    /// <summary>Returns application name, security mode/policy, and endpoint URL.</summary>
    public override string ToString()
    {
        EndpointDescription ep = OpcUaEndpointDescription;

        return $"AppName={ep.Server.ApplicationName}; " +
            $"Security={ep.SecurityMode}; " +
            $"Policy={ep.SecurityPolicyUri.Split("#").LastOrDefault() ?? string.Empty}; " +
            $"EndpointUrl={EndpointUrl}; ";
    }
    #endregion

    #region private methods
    private int GetSecurityLevel()
    {
        return OpcUaEndpointDescription.SecurityLevel;
    }
    #endregion
}
