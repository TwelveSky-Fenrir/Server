using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.FriendLocate, ExpectedSize = 9)]
public readonly partial record struct FriendLocateResponse : IOutgoingPacket
{
    public required int Index { get; init; }
    public required int ZoneNumber { get; init; }
}
