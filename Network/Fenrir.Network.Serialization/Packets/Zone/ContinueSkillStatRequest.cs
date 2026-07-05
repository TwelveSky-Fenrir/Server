using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// Registers up to MAX_AUTO_BUFF_SKILL_NUM (8) auto-buff skill slots; Skill is int[8][2] flattened
// (Skill[i*2]=skillId, Skill[i*2+1]=requested grade). Always replies Result=0, even for unlearned skills.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.ContinueSkillStat,
    ExpectedSize = 73, AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct ContinueSkillStatRequest : IIncomingPacket<ContinueSkillStatRequest>
{
    [FixedArray(16)] public required int[] Skill { get; init; }
}
