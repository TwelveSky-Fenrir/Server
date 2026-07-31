using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout ZC_ONLINE_EVENT_RECV Server/Header/Protocol/ZONE.h:1118-1127, sept champs : un de plus que ZC_PCROOM_PET_RECV ZONE.h:1108-1116 dont il partage les cinq premiers ; mort en M33/LNW33 : orphelin total, les seules occurrences dans Server/ sont les trois lignes de ZONE.h (struct, ZCP:1699, ZCS:1700).
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.OnlineTimeReward,
    ExpectedSize = 29)]
public readonly partial record struct OnlineTimeRewardResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int ItemIndex { get; init; }

    public required int Page { get; init; }

    public required int Index { get; init; }

    public required int Xy { get; init; }

    public required int PlayOnlineTime { get; init; }

    public required int PlayOnlineTime2 { get; init; }
}
