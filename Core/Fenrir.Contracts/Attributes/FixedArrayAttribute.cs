namespace Fenrir.Contracts.Attributes;

/// <summary>
///     Array field (<c>int[]</c>/<c>float[]</c>/<c>byte[]</c>) representing a C++ array, flattened
///     row-major for multi-dimensional cases (e.g. <c>int[2][64][6]</c> → 768 elements).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FixedArrayAttribute(int elementCount) : Attribute
{
    public int ElementCount { get; } = elementCount;
}
