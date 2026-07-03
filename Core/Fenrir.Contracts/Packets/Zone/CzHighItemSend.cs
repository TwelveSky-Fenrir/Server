using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_HIGH_ITEM_SEND (CLIENT.h:264) — same typedef as <see cref="CzImproveItemSend" /> (24). Rank
///     upgrade (requires +4 / combine >= 1); Warlord variant under <c>__REBIRTH__</c> (active). Response: ZC 30.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.HighItemSend, ExpectedSize = 29,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzHighItemSend : IIncomingPacket<CzHighItemSend>
{
    public required int Page1 { get; init; }

    public required int Index1 { get; init; }

    public required int Page2 { get; init; }

    public required int Index2 { get; init; }

    public required int Luck { get; init; }
}
