using Microsoft.CodeAnalysis;

namespace Fenrir.Generators.Analysis.Model;

/// <summary>
///     A resolved field (record struct property) ready to be offset/emitted. <see cref="Offset" />/<see cref="OwnSize" />
///     are computed by <c>Fenrir.Generators.Protocol.Analysis.FieldScanner</c> in one cumulative pass over the
///     properties' source declaration order — THAT ORDER is the binary layout, never reorder it.
/// </summary>
internal sealed class FieldModel
{
    public required string PropertyName { get; init; }

    public required IPropertySymbol Symbol { get; init; }

    public required FieldShape Shape { get; init; }

    /// <summary>Padding bytes from <see cref="Attributes.ReservedAttribute" /> inserted BEFORE this field.</summary>
    public int ReservedBefore { get; init; }

    /// <summary>
    ///     Element count for <see cref="FieldShape.Int32Array" />/<see cref="FieldShape.SingleArray" />/
    ///     <see cref="FieldShape.ByteArray" />/<see cref="FieldShape.FixedStringArray" />.
    /// </summary>
    public int ElementCount { get; init; }

    /// <summary>
    ///     Length in bytes of a string (<see cref="FieldShape.FixedString" />) or a row (
    ///     <see cref="FieldShape.FixedStringArray" />).
    /// </summary>
    public int StringLength { get; init; }

    /// <summary>Fully qualified nested type name for <see cref="FieldShape.Nested" />.</summary>
    public string? NestedTypeFullName { get; init; }

    /// <summary>Resolved <c>WireSize</c> of the nested type (§4: detected via <c>IFenrirWireType&lt;T&gt;</c>).</summary>
    public int NestedSize { get; init; }

    /// <summary>Carries <c>[ObfuscatedUidField]</c> (only <c>LC_LOGIN_RECV.Id</c>, §3.3).</summary>
    public bool IsLegacyUidField { get; init; }

    /// <summary><c>[AvatarXorKind]</c> — <c>None</c> if absent (only <c>AvatarRosterResponse</c> uses it, §3.2).</summary>
    public AvatarXorKind AvatarXor { get; init; } = AvatarXorKind.None;

    public int AvatarXorRowLength { get; init; }

    /// <summary>Cumulative offset (after <see cref="ReservedBefore" />), computed by <c>FieldScanner</c>.</summary>
    public int Offset { get; set; }

    /// <summary>This field's own size (excluding <see cref="ReservedBefore" />).</summary>
    public int OwnSize { get; init; }
}
