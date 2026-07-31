using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout ZC_LIMIT_MONSTER_SUMMON_LIMIT_RECV Server/Header/Protocol/ZONE.h:1239-1243 ; mort en M33/LNW33 : jamais implemente, les 3 seules references du symbole sont Server/Header/Protocol/ZONE.h:1243, :1679 et :1680, aucun emetteur ni appelant.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.MonsterSummonLimit,
    ExpectedSize = 9)]
public readonly partial record struct MonsterSummonLimitResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Result2 { get; init; }
}
