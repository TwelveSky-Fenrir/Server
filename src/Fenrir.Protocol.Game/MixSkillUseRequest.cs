using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Dead opcode in both shipped builds: never registered via REGWORK1 (Server/ts25zone/S04_MyWork01.cpp); P_MIXSKILL_USE_SEND = 128 (Server/Header/Protocol/CLIENT.h:736); a real client never sends it
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.MixSkillUse, ExpectedSize = 9,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct MixSkillUseRequest : IIncomingPacket<MixSkillUseRequest>
{
}
