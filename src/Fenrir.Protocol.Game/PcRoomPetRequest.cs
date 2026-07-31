using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout CZ_PCROOM_PET_SEND CLIENT.h:155-161 (corps vide, CLIENT_PACKET nu); mort en M33/LNW33: opcode 136 non enregistre dans W_FUNCTION.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.PcRoomPet, ExpectedSize = 9,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct PcRoomPetRequest : IIncomingPacket<PcRoomPetRequest>
{
}
