using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_SET_HOTKEY_INVENTORY_RECV (ZONE.h:1129-1138) — unique emitter:
///     <c>AVATAR_OBJECT::BotHotKeySend(int tPillType)</c> (S07_MyGame04.cpp:2534-2559): the auto-hunt bot
///     re-binds the hotkey to the next stack after a pill is consumed. Unicast (server push, no client
///     request).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.AutoHuntHotkeyRebind,
    ExpectedSize = 29)]
public readonly partial record struct AutoHuntHotkeyRebindResponse : IOutgoingPacket
{
    public required int Page1 { get; init; }

    public required int Index1 { get; init; }

    public required int Page2 { get; init; }

    public required int Index2 { get; init; }

    public required int Value0 { get; init; }

    public required int Value1 { get; init; }

    public required int Value2 { get; init; }
}
