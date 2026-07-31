using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.FriendRemove, ExpectedSize = 5)]
public readonly partial record struct FriendRemoveResponse : IOutgoingPacket
{
    public required int Index { get; init; }
}
