using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GetCashBalance, ExpectedSize = 13,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct GetCashBalanceRequest : IIncomingPacket<GetCashBalanceRequest>
{
    public required int Sort { get; init; }
}
