using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_DESTROY_ITEM_SEND (CLIENT.h:231) — same typedef as <see cref="CzUseHotkeyItemSend" /> (22).
///     Voluntary destruction of an upgraded Rare/Elite item -> refunds money + compensation stone.
///     Response: ZC 106.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.DestroyItemSend, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzDestroyItemSend : IIncomingPacket<CzDestroyItemSend>
{
    public required int Page1 { get; init; }

    public required int Index1 { get; init; }
}
