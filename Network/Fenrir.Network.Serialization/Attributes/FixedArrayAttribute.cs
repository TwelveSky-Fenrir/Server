namespace Fenrir.Network.Serialization.Attributes;

/// <summary>C++ array field, flattened row-major for multi-dimensional cases (e.g. <c>int[2][64][6]</c> → 768 elements).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FixedArrayAttribute(int elementCount) : Attribute
{
    public int ElementCount { get; } = elementCount;
}
