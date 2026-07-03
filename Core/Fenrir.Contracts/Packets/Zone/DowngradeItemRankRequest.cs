using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_LOW_ITEM_SEND (CLIENT.h:264) — same typedef as <see cref="EnchantItemRequest" /> (24). Symmetric
///     downgrade (materials 1024/1025/984). Response: ZC 31.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.DowngradeItemRank, ExpectedSize = 29,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct DowngradeItemRankRequest : IIncomingPacket<DowngradeItemRankRequest>
{
    public required int Page1 { get; init; }

    public required int Index1 { get; init; }

    public required int Page2 { get; init; }

    public required int Index2 { get; init; }

    public required int Luck { get; init; }
}
