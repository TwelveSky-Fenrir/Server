namespace Fenrir.Generators.Analysis.Model;

/// <summary>Discovered <c>IInlinePacketHandler&lt;T&gt;</c>/<c>IAsyncPacketHandler&lt;T&gt;</c> handler.</summary>
internal sealed class HandlerModel
{
    public required string HandlerTypeFullName { get; init; }

    public required string PacketTypeFullName { get; init; }

    public required FenrirServer Server { get; init; }

    public required byte Opcode { get; init; }

    public required bool IsAsync { get; init; }
}
