using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_MAKE_ITEM2_RECV (ZONE.h:1357) — same typedef as <see cref="ZcUpLevelItemRecv" /> (164),
///     including the dead trailing <see cref="Padding" /> byte (wire size 30, not 29 — see that type's
///     remarks). Reply to CZ_MAKE_ITEM2_SEND (131) + <c>MakeNotice</c> announcement on success; unicast.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.MakeItem2Recv, ExpectedSize = 30)]
public readonly partial record struct ZcMakeItem2Recv : IOutgoingPacket
{
    public required int Result { get; init; }

    [FixedArray(6)] public required int[] Value { get; init; }

    /// <summary>Dead trailing byte (<c>BYTE tmp</c>), always written as 0.</summary>
    public required byte Padding { get; init; }
}
