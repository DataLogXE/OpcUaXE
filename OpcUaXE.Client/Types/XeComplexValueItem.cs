namespace OpcUaXE.Client.Types;

/// <summary>
/// Represents a complex value item with a name, path, type, and associated value.
/// </summary>
public sealed class XeComplexValueItem
{
    #region public properties

    /// <summary>Property name within the complex structure.</summary>
    public string Name { get; internal set; } = string.Empty;

    /// <summary>Hierarchical path segments leading to this property.</summary>
    public string[] Path { get; internal set; } = [];

    /// <summary>CLR type of the value.</summary>
    public Type? Type { get; internal set; }

    /// <summary>
    /// Gets the wrapped value associated with the current instance.
    /// </summary>
    public XeValueWrapper Value
    {
        get
        {
            _wrapper ??= new XeValueWrapper(RawValue);
            return _wrapper;
        }
    }
    #endregion

    #region internal properties

    /// <summary>Underlying raw value before wrapping.</summary>
    internal object? RawValue { get; set; }
    #endregion

    #region private fields
    private XeValueWrapper? _wrapper;
    #endregion

    #region public methods

    /// <inheritdoc />
    public override string ToString()
    {
        string pathStr = Path.Length > 0 ? string.Join(".", Path) + "." : string.Empty;
        return $"Path={pathStr + Name}; Type={Type}; Value={Value}; ";
    }
    #endregion
}
