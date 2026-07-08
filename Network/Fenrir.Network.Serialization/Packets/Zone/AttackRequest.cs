using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// mCase outside 1..6 must Quit() the session; server must recompute result fields, never trust client values.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.Attack, ExpectedSize = 77,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly record struct AttackRequest : IIncomingPacket<AttackRequest>
{
    public required AttackForProtocol AttackInfo { get; init; }
}
