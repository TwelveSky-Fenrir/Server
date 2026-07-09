using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

// Registers up to MAX_AUTO_BUFF_SKILL_NUM (8) auto-buff skill slots; Skill is int[8][2] flattened
// (Skill[i*2]=skillId, Skill[i*2+1]=requested grade). Always replies Result=0, even for unlearned skills.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.ContinueSkillStat,
    ExpectedSize = 73, AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly record struct ContinueSkillStatRequest : IIncomingPacket<ContinueSkillStatRequest>
{
    [FixedArray(16)] public required int[] Skill { get; init; }
}
