using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_USE_HOTKEY_ITEM_SEND (CLIENT.h:227-231) — typedef SHARED with <see cref="CzDestroyItemSend" />
///     (op 89): identical layout, distinct C# contracts. Response: ZC 25.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.UseHotkeyItemSend, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzUseHotkeyItemSend : IIncomingPacket<CzUseHotkeyItemSend>
{
    public required int Page1 { get; init; }

    public required int Index1 { get; init; }
}
