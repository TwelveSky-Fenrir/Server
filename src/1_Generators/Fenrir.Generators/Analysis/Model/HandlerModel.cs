using Microsoft.CodeAnalysis;

namespace Fenrir.Generators.Analysis.Model;

internal readonly record struct HandlerModel
{
    public required string HandlerTypeFullName { get; init; }

    public required string PacketTypeFullName { get; init; }

    public required FenrirServer Server { get; init; }

    public required byte Opcode { get; init; }

    public required bool IsAsync { get; init; }

    public required Location Location { get; init; }
}
