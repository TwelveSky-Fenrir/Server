using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

// Sort: 1=close local personal shop, 2=close proxy shop.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.CloseShopStall, ExpectedSize = 13,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CloseShopStallRequest : IIncomingPacket<CloseShopStallRequest>
{
    public required int Sort { get; init; }
}
