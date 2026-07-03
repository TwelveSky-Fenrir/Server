using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_AUTO_CONFIG_RECV (ZONE.h:1147-1152) — builder named <c>B_AUTO_HUNT_RECV</c>
///     (S05_MyTransfer.cpp:1456). Two live emitters: end of the CZ 99 handler
///     (S04_MyWork02.cpp:13613, <c>tAutoState = r->tSort</c>) and personal-shop open
///     <c>W_START_PSHOP_SEND</c> (S04_MyWork02.cpp:6327, forces <c>aAutoState = 0</c>). The
///     (ServerIndex/UniqueNumber) shape suggests a broadcast origin, reduced to unicast in this build.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.AutoHuntToggle, ExpectedSize = 13)]
public readonly partial record struct AutoHuntToggleResponse : IOutgoingPacket
{
    public required int ServerIndex { get; init; }
    public required uint UniqueNumber { get; init; }
    public required int AutoState { get; init; }
}
