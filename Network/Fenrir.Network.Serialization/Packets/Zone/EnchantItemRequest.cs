using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// Page1/Index1 = target slot, Page2/Index2 = material slot, Luck = lucky-item flag.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.EnchantItem, ExpectedSize = 29,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct EnchantItemRequest : IIncomingPacket<EnchantItemRequest>
{
    public required int Page1 { get; init; }

    public required int Index1 { get; init; }

    public required int Page2 { get; init; }

    public required int Index2 { get; init; }

    public required int Luck { get; init; }
}
