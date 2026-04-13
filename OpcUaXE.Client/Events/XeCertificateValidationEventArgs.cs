using System.Security.Cryptography.X509Certificates;

namespace OpcUaXE.Client.Events;

/// <summary>
/// Event data raised when the OPC UA stack requests server certificate validation.
/// </summary>
/// <param name="certificate">Certificate presented by the server.</param>
public sealed class XeCertificateValidationEventArgs(X509Certificate2 certificate) : EventArgs
{
    #region public properties
    /// <summary>Gets the server certificate to be validated.</summary>
    public X509Certificate2 Certificate { get; } = certificate;

    /// <summary>Set to <see langword="true"/> to accept the certificate for this session.</summary>
    public bool Accept { get; set; } = false;

    /// <summary>Set to <see langword="true"/> to add the certificate to the trusted store permanently.</summary>
    public bool AcceptPermanently { get; set; } = false;
    #endregion
}
