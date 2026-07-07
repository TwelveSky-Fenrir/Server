using Microsoft.CodeAnalysis;

namespace Fenrir.Generators.Analysis.Model;

/// <summary>
///     Resolved model of a <c>readonly partial record struct</c> carrying <c>[FenrirPacket]</c>/
///     <c>[FenrirWireType]</c>.
/// </summary>
/// <remarks>
///     A <c>record</c> (not <c>class</c>) so consecutive <c>IIncrementalGenerator</c> passes that produce a
///     structurally identical <see cref="TypeModel" /> compare equal and Roslyn can skip redundant downstream
///     work (<c>OpcodeCollisionChecker.Check</c>, the aggregate emitters). Its former <c>Symbol</c>
///     (<see cref="INamedTypeSymbol" />) property was removed rather than kept alongside record conversion:
///     it had zero readers anywhere in either generator (verified by repo-wide grep before deletion), and
///     symbols are never meaningfully equatable across compilations — keeping it would have silently defeated
///     the whole point of this conversion by rooting an old <c>Compilation</c> in every cached instance.
///     <see cref="Location" /> is deliberately kept despite the same caching caveat (Roslyn's own cookbook
///     recommends removing <c>Location</c> members too, since edits change a syntax tree's identity): unlike
///     <c>Symbol</c>, it is genuinely read downstream — <c>OpcodeCollisionChecker</c> needs it to anchor the
///     <c>FEN004</c> diagnostic on the colliding type's declaration — and there is no clean way to carry a
///     reportable diagnostic location without it.
/// </remarks>
internal sealed record TypeModel
{
    public required string Namespace { get; init; }

    public required string TypeName { get; init; }

    public required string FullTypeName { get; init; }

    /// <summary>
    ///     <c>true</c> = <c>[FenrirPacket]</c> (emits Opcode/PayloadSize); <c>false</c> = <c>[FenrirWireType]</c> (emits
    ///     WireSize).
    /// </summary>
    public required bool IsPacket { get; init; }

    public FenrirServer Server { get; init; }

    public FenrirDirection Direction { get; init; }

    public byte Opcode { get; init; }

    public WireObfuscationMode Obfuscation { get; init; }

    public bool Compressed { get; init; }

    public int ExpectedSize { get; init; } = -1;

    public EquatableArray<byte> AllowedStates { get; init; } = EquatableArray<byte>.Empty;

    public required bool ImplementsIncoming { get; init; }

    public required bool ImplementsOutgoing { get; init; }

    public required EquatableArray<FieldModel> Fields { get; init; }

    /// <summary>Sum of own fields (+ padding); EXCLUDES the header for packets.</summary>
    public required int FieldsSize { get; init; }

    /// <summary>Final emitted <c>PayloadSize</c>/<c>WireSize</c>; alias of <see cref="FieldsSize" />.</summary>
    public int PayloadOrWireSize => FieldsSize;

    public required Location Location { get; init; }
}
