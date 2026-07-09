using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

// Only Sort==0 succeeds (Value=bottle slot 0..9), requiring stock>0 and (no mount or absorption active); invalid Sort returns an error response, not Quit().
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.DrinkBottle, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly record struct DrinkBottleRequest : IIncomingPacket<DrinkBottleRequest>
{
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
