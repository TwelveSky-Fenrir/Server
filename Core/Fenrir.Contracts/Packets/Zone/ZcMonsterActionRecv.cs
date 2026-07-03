using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_MONSTER_ACTION_RECV (ZONE.h:427-433, 124-byte payload: 4 + 4 + <see cref="ObjectForMonster" /> (112) + 4) —
///     monster replication (AI ticks, summons, periodic 5s logic tick). No <c>#ifdef</c> in this struct: stable
///     layout across all builds. AOI broadcast.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.MonsterActionRecv,
    ExpectedSize = 125)]
public readonly partial record struct ZcMonsterActionRecv : IOutgoingPacket
{
    public required int ServerIndex { get; init; }
    public required uint UniqueNumber { get; init; }
    public required ObjectForMonster Data { get; init; }

    /// <summary>0/1/2: forces animation re-sync client-side.</summary>
    public required int CheckChangeActionState { get; init; }
}
