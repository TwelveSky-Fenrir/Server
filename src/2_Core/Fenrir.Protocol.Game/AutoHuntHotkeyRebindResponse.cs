using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

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
