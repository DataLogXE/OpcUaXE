using Opc.Ua;

namespace OpcUaXE.Client.Types;

/// <summary>
/// Wraps an OPC UA <see cref="Opc.Ua.StatusCode"/>, exposing quality flags and the raw code.
/// </summary>
public sealed class XeValueState
{
    /// <summary><see langword="true"/> when the status code indicates good quality.</summary>
    public bool IsGood => StatusCode.IsGood(_statusCode);

    /// <summary><see langword="true"/> when the status code indicates an error or bad quality.</summary>
    public bool IsBad => StatusCode.IsBad(_statusCode);

    /// <summary><see langword="true"/> when the status code indicates uncertain quality.</summary>
    public bool IsUncertain => StatusCode.IsUncertain(_statusCode);

    /// <summary>Raw OPC UA numeric status code value.</summary>
    public uint StatusCodeValue => _statusCode.Code;

    private readonly StatusCode _statusCode;

    internal XeValueState()
    {
        _statusCode = StatusCodes.Good;
    }

    internal XeValueState(StatusCode statusCode)
    {
        _statusCode = statusCode;
    }

    /// <summary>Creates a <see cref="XeValueState"/> representing a good-quality result.</summary>
    public static XeValueState Good() => new(StatusCodes.Good);

    /// <summary>Creates a <see cref="XeValueState"/> representing a bad-quality result.</summary>
    public static XeValueState Bad() => new(StatusCodes.Bad);

    /// <summary>Creates a <see cref="XeValueState"/> representing an uncertain-quality result.</summary>
    public static XeValueState Uncertain() => new(StatusCodes.Uncertain);

    /// <inheritdoc />
    public override string ToString() => _statusCode.ToString();
}
