using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_RUNE_SYSTEM_RECV (ZONE.h:495-502) — reply to CZ_RUNE_SYSTEM_SEND (157); unicast.
///     <see cref="Result" />: 0 = removal ok, 1 = insertion ok, 2 = error (inventory full). Same layout
///     as <c>ZC_RUNE_PROCESSDATA_RECV</c> (201, DEAD — see the dead-opcodes section). NOTE: field order
///     DIFFERS from CZ 157 (see <see cref="CzRuneSystemSend" /> remarks).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.RuneSystemRecv, ExpectedSize = 21)]
public readonly partial record struct ZcRuneSystemRecv : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Page { get; init; }

    public required int Index { get; init; }

    public required int ItemIndex { get; init; }

    public required int RuneIndex { get; init; }
}
