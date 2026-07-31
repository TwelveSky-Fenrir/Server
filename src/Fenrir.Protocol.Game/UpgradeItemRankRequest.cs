using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.UpgradeItemRank, ExpectedSize = 29,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct UpgradeItemRankRequest : IIncomingPacket<UpgradeItemRankRequest>
{
    public required int Page1 { get; init; }

    public required int Index1 { get; init; }

    public required int Page2 { get; init; }

    public required int Index2 { get; init; }

    public required int Luck { get; init; }
}
