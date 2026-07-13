using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;
using Fenrir.Application.Game.ZoneRuntime;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.UseInventoryItem,
    ExpectedSize = 21, AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct UseInventoryItemRequest : IIncomingPacket<UseInventoryItemRequest>
{
    public required int Page { get; init; }

    public required int Index { get; init; }

    public required int Value { get; init; }
}
