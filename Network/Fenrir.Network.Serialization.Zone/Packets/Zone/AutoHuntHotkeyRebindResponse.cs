using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

// Server-initiated push (no client request) — auto-hunt bot rebinds hotkey after consuming a pill.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.AutoHuntHotkeyRebind,
    ExpectedSize = 29)]
public readonly record struct AutoHuntHotkeyRebindResponse : IOutgoingPacket
{
    public required int Page1 { get; init; }

    public required int Index1 { get; init; }

    public required int Page2 { get; init; }

    public required int Index2 { get; init; }

    public required int Value0 { get; init; }

    public required int Value1 { get; init; }

    public required int Value2 { get; init; }
}
