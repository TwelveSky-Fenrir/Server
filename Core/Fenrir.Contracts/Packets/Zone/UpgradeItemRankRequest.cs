using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>Rank upgrade requires the item to be +4 already (combine ≥ 1).</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.UpgradeItemRank, ExpectedSize = 29,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct UpgradeItemRankRequest : IIncomingPacket<UpgradeItemRankRequest>
{
    public required int Page1 { get; init; }

    public required int Index1 { get; init; }

    public required int Page2 { get; init; }

    public required int Index2 { get; init; }

    public required int Luck { get; init; }
}
