using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.UseHotkeyItem, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct UseHotkeyItemRequest : IIncomingPacket<UseHotkeyItemRequest>
{
    public required int Page1 { get; init; }

    public required int Index1 { get; init; }
}
