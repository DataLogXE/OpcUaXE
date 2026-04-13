using System.Collections;

namespace OpcUaXE.Client.Types;

/// <summary>
/// A read-only, ordered collection of <see cref="XeComplexValueItem"/> objects that represents
/// the flattened properties of an OPC UA complex or array value.
/// </summary>
public sealed class XeComplexValueCollection : IReadOnlyList<XeComplexValueItem>
{
    private readonly List<XeComplexValueItem> _items = [];

    /// <inheritdoc />
    public XeComplexValueItem this[int index] => _items[index];

    /// <inheritdoc />
    public int Count => _items.Count;

    /// <inheritdoc />
    public IEnumerator<XeComplexValueItem> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    #region Internal Build API (XeExtensions)

    internal Stack<string> Path { get; } = new();

    internal void PushPath(string name) => Path.Push(name);

    internal string PopPath() => Path.Pop();

    internal void AddItem(object? value, Type type, string name)
    {
        _items.Add(new XeComplexValueItem
        {
            Name = name,
            Type = type,
            RawValue = value,
            Path = Path.Reverse().ToArray()
        });
    }

    #endregion
}
