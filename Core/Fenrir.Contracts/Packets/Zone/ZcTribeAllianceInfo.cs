using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_TRIBE_ALLIANCE_INFO (ZONE.h:912-916, builder :1124-1129) — server-initiated event (never a
///     direct reply), emitted at 5 sites in the inter-tribe alliance state machine
///     (S07_MyGame01.cpp:3893/3944/3951/3982/3989) to both "alliance post" avatars (Force Leaders).
///     <c>Sort</c>: 1 open (Value=0), 2 phase-1 tick (Value=remaining time), 3 phase-1 cancel (Value=0),
///     4 phase-2 tick (Value=remaining time), 5 phase-2 cancel (Value=0).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribeAllianceInfo, ExpectedSize = 9)]
public readonly partial record struct ZcTribeAllianceInfo : IOutgoingPacket
{
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
