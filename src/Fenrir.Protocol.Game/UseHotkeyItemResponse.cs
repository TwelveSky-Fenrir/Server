using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.UseHotkeyItem,
    ExpectedSize = 13)]
public readonly partial record struct UseHotkeyItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Page { get; init; }

    public required int Index { get; init; }
}
