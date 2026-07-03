using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Fenrir.Generators.Analysis.Model;

/// <summary>
///     Resolved model of a <c>readonly partial record struct</c> carrying <c>[FenrirPacket]</c> or
///     <c>[FenrirWireType]</c>.
/// </summary>
internal sealed class TypeModel
{
    public required INamedTypeSymbol Symbol { get; init; }

    public required string Namespace { get; init; }

    public required string TypeName { get; init; }

    public required string FullTypeName { get; init; }

    /// <summary>
    ///     <c>true</c> = <c>[FenrirPacket]</c> (emits <c>Opcode</c>/<c>PayloadSize</c>); <c>false</c> =
    ///     <c>[FenrirWireType]</c> (emits <c>WireSize</c>).
    /// </summary>
    public required bool IsPacket { get; init; }

    public FenrirServer Server { get; init; }

    public FenrirDirection Direction { get; init; }

    public byte Opcode { get; init; }

    public LegacyObfuscation Obfuscation { get; init; }

    public bool Compressed { get; init; }

    public int ExpectedSize { get; init; } = -1;

    public ImmutableArray<byte> AllowedStates { get; init; } = ImmutableArray<byte>.Empty;

    public required bool ImplementsIncoming { get; init; }

    public required bool ImplementsOutgoing { get; init; }

    public required ImmutableArray<FieldModel> Fields { get; init; }

    /// <summary>Sum of own fields (+ padding); EXCLUDES the header for packets.</summary>
    public required int FieldsSize { get; init; }

    /// <summary>
    ///     Final emitted <c>PayloadSize</c>/<c>WireSize</c> (== <see cref="FieldsSize" />, readability alias at the call
    ///     site).
    /// </summary>
    public int PayloadOrWireSize => FieldsSize;

    public required Location Location { get; init; }
}
