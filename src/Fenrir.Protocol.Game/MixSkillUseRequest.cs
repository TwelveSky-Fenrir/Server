using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// CLIENT.h:155-161 CZ_MIXSKILL_USE_SEND corps vide; mort: aucun handler ni REGWORK dans ts25zone, W_FUNCTION[128] nul.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.MixSkillUse, ExpectedSize = 9,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct MixSkillUseRequest : IIncomingPacket<MixSkillUseRequest>
{
}
