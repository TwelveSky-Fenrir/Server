using Fenrir.Core.Attributes;
using Fenrir.Core.Packets.Shared;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.OpenShopStall, ExpectedSize = 1245,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct OpenShopStallRequest : IIncomingPacket<OpenShopStallRequest>
{
    public required int Sort { get; init; }
    public required PshopInfo PshopInfo { get; init; }
}
