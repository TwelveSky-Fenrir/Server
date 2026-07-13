using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;
using Fenrir.Application.Game.ZoneRuntime;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.BuyBloodMarkItem, ExpectedSize = 45,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct BuyBloodMarkItemRequest : IIncomingPacket<BuyBloodMarkItemRequest>
{
    public required int BloodIndex { get; init; }
    public required int Page { get; init; }
    public required int Index { get; init; }
    [FixedArray(6)] public required int[] Value { get; init; }
}
