using Opc.Ua;
using OpcUaXE.Client.Types;
using System.Reflection;

namespace OpcUaXE.Client.Core.Helper;

internal static class XeExtensions
{
    #region public methods
    /// <summary>Enumerates all property values of the wrapped complex type.</summary>
    /// <param name="extensionObject">OPC UA extension object to enumerate.</param>
    /// <returns>Flat collection of all scalar leaf values.</returns>
    public static XeComplexValueCollection Enumerate(this ExtensionObject extensionObject)
    {
        XeComplexValueCollection result = [];
        EnumerateObjectInternal(extensionObject, result);
        return result;
    }

    /// <summary>Enumerates all elements of an array as flat value items.</summary>
    /// <param name="array">Array to enumerate.</param>
    /// <returns>Flat collection of all array elements.</returns>
    public static XeComplexValueCollection Enumerate(this Array array)
    {
        XeComplexValueCollection result = [];
        EnumerateArrayInternal(array, "", result);
        return result;
    }
    #endregion

    #region private methods
    private static void EnumerateObjectInternal(object? obj, XeComplexValueCollection result)
    {
        if (obj == null) return;

        if (obj is ExtensionObject extensionObject)
        {
            obj = extensionObject.Body;
            if (obj == null) return;
        }

        PropertyInfo[] props = obj.GetType().GetProperties()
            .Where(p => p.DeclaringType == obj.GetType()).ToArray();

        foreach (PropertyInfo prop in props)
        {
            object? o = prop.GetValue(obj);
            if (IsSimpleType(prop.PropertyType))
            {
                result.AddItem(o, prop.PropertyType, prop.Name);
            }
            else if (o is Array array)
            {
                EnumerateArrayInternal(array, prop.Name, result);
            }
            else if (o != null)
            {
                result.PushPath(prop.Name);
                EnumerateObjectInternal(o, result);
                result.PopPath();
            }
        }
    }

    private static void EnumerateArrayInternal(Array arr, string name, XeComplexValueCollection result)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            object? item = arr.GetValue(i);
            if (item == null) continue;

            Type t = item.GetType();
            if (IsSimpleType(t))
            {
                result.AddItem(item, t, $"{name}[{i}]");
            }
            else
            {
                result.PushPath($"{name}[{i}]");
                EnumerateObjectInternal(item, result);
                result.PopPath();
            }
        }
    }

    private static bool IsSimpleType(Type type)
    {
        return
        type.IsPrimitive ||
        type == typeof(string);
    }
    #endregion
}
