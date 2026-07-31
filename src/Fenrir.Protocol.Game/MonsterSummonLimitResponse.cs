using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.MonsterSummonLimit,
    ExpectedSize = 9)]
public readonly partial record struct MonsterSummonLimitResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Result2 { get; init; }
}
