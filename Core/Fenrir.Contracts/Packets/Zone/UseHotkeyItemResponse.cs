using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>ZC_USE_HOTKEY_ITEM_RECV (ZONE.h:477-482) — reply to CZ_USE_HOTKEY_ITEM_SEND (22); unicast.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.UseHotkeyItem,
    ExpectedSize = 13)]
public readonly partial record struct UseHotkeyItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Page { get; init; }

    public required int Index { get; init; }
}
