using Microsoft.CodeAnalysis;

namespace Fenrir.Generators.Analysis.Model;

internal sealed record TypeModel
{
    public required string Namespace { get; init; }

    public required string TypeName { get; init; }

    public required string FullTypeName { get; init; }

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

    public required int FieldsSize { get; init; }

    public int PayloadOrWireSize => FieldsSize;

    public required LocationInfo? Location { get; init; }
}
