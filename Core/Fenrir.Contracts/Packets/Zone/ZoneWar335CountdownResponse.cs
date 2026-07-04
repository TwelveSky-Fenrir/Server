using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>Twin countdown ZC 198 (335_TYPE_BATTLE_COUNTDOWN) is dead; FFA only emits this one.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar335Countdown,
    ExpectedSize = 5)]
public readonly partial record struct ZoneWar335CountdownResponse : IOutgoingPacket
{
    public required int RemainTime { get; init; }
}
