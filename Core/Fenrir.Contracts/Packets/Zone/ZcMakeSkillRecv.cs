using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_MAKE_SKILL_RECV (ZONE.h:537) — same typedef as <see cref="ZcMakeItemRecv" /> (32). Reply to
///     CZ_MAKE_SKILL_SEND (30); unicast.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.MakeSkillRecv, ExpectedSize = 29)]
public readonly partial record struct ZcMakeSkillRecv : IOutgoingPacket
{
    public required int Result { get; init; }

    [FixedArray(6)] public required int[] Value { get; init; }
}
