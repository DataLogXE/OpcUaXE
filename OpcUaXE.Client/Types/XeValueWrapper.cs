using System.Collections;

namespace OpcUaXE.Client.Types;

/// <summary>
/// Wraps an OPC UA node value and provides:
/// <list type="bullet">
/// <item>Implicit conversions to all common OPC UA built-in scalar types for ergonomic access.</item>
/// <item>A generic <see cref="GetValue{T}(T)"/> method for explicit, type-safe retrieval.</item>
/// </list>
/// Conversions that cannot be performed return the supplied default value instead of throwing.
/// </summary>
public sealed class XeValueWrapper
{
    private readonly object? _value;

    internal XeValueWrapper(object? value)
    {
        _value = value;
    }

    /// <summary>
    /// Returns the wrapped value cast to <typeparamref name="T"/>, or <paramref name="defaultValue"/>
    /// when the value is <see langword="null"/> or the conversion fails.
    /// </summary>
    /// <typeparam name="T">Target type.</typeparam>
    /// <param name="defaultValue">Value returned when conversion is not possible. Defaults to <c>default(T)</c>.</param>
    public T GetValue<T>(T defaultValue = default!) => As(defaultValue);

    /// <summary>Returns the raw underlying value. May be <see langword="null"/>.</summary>
    public object? GetRawValue() => _value;

    #region Implicit Conversions

    /// <summary>Implicitly converts to <see cref="bool"/>; returns <see langword="false"/> on failure.</summary>
    public static implicit operator bool(XeValueWrapper v) => v.As(false);

    /// <summary>Implicitly converts to <see cref="sbyte"/>; returns <c>0</c> on failure.</summary>
    public static implicit operator sbyte(XeValueWrapper v) => v.As((sbyte)0);

    /// <summary>Implicitly converts to <see cref="byte"/>; returns <c>0</c> on failure.</summary>
    public static implicit operator byte(XeValueWrapper v) => v.As((byte)0);

    /// <summary>Implicitly converts to <see cref="short"/>; returns <c>0</c> on failure.</summary>
    public static implicit operator short(XeValueWrapper v) => v.As((short)0);

    /// <summary>Implicitly converts to <see cref="ushort"/>; returns <c>0</c> on failure.</summary>
    public static implicit operator ushort(XeValueWrapper v) => v.As((ushort)0);

    /// <summary>Implicitly converts to <see cref="int"/>; returns <c>0</c> on failure.</summary>
    public static implicit operator int(XeValueWrapper v) => v.As(0);

    /// <summary>Implicitly converts to <see cref="uint"/>; returns <c>0</c> on failure.</summary>
    public static implicit operator uint(XeValueWrapper v) => v.As(0u);

    /// <summary>Implicitly converts to <see cref="long"/>; returns <c>0</c> on failure.</summary>
    public static implicit operator long(XeValueWrapper v) => v.As(0L);

    /// <summary>Implicitly converts to <see cref="ulong"/>; returns <c>0</c> on failure.</summary>
    public static implicit operator ulong(XeValueWrapper v) => v.As(0ul);

    /// <summary>Implicitly converts to <see cref="float"/>; returns <c>0f</c> on failure.</summary>
    public static implicit operator float(XeValueWrapper v) => v.As(0f);

    /// <summary>Implicitly converts to <see cref="double"/>; returns <c>0.0</c> on failure.</summary>
    public static implicit operator double(XeValueWrapper v) => v.As(0d);

    /// <summary>Implicitly converts to <see cref="DateTime"/> (UTC); returns <see cref="DateTime.MinValue"/> on failure.</summary>
    public static implicit operator DateTime(XeValueWrapper v) => v.As(DateTime.MinValue);

    /// <summary>
    /// Implicitly converts to <see cref="string"/>;
    /// returns <see cref="string.Empty"/> when the value is <see langword="null"/>.
    /// </summary>
    public static implicit operator string(XeValueWrapper v) => v._value?.ToString() ?? string.Empty;

    /// <inheritdoc />
    public override string ToString() => _value?.ToString() ?? string.Empty;

    #endregion

    #region Internal Helper

    private T As<T>(T defaultValue)
    {
        try
        {
            if (_value is null)
                return defaultValue;

            if (_value is T direct)
                return direct;

            // Handle IEnumerable<T> edge-case (e.g. byte[] → T[])
            if (_value is IEnumerable && typeof(T).IsArray)
                return defaultValue;

            return (T)Convert.ChangeType(_value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    #endregion
}
