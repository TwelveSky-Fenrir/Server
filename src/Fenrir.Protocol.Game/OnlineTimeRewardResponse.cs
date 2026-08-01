using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

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
