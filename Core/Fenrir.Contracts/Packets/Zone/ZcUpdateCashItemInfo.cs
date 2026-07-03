using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_UPDATE_CASH_ITEM_INFO (ZONE.h:1043-1045) — bare invalidation signal: the client must re-issue
///     CZ_GET_CASH_ITEM_INFO_SEND. Empty payload (opcode byte only). Pushed from the game loop whenever
///     <c>mCashInfo-&gt;mVersion</c> changes; emitted per-user in a loop over all users, a de facto broadcast.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.UpdateCashItemInfo,
    ExpectedSize = 1)]
public readonly partial record struct ZcUpdateCashItemInfo : IOutgoingPacket
{
}
