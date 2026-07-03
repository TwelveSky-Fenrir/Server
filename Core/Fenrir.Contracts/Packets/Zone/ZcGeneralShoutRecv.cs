using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_GENERAL_SHOUT_RECV (ZONE.h:615-622, <c>ZCS_GENERAL_SHOUT_RECV</c>) — zone-wide shout,
///     <c>BroadcastServer(1)</c> (whole zone). Distinct C++ struct from ZC 41 but byte-identical layout.
///     Emitted only by zones 37/119/124/84 (runtime condition of CZ 40).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GeneralShoutRecv, ExpectedSize = 99)]
public readonly partial record struct ZcGeneralShoutRecv : IOutgoingPacket
{
    [FixedString(13)] public required string AvatarName { get; init; }
    [FixedString(61)] public required string Content { get; init; }
    public required ItemLinkInfo Link { get; init; }
}
