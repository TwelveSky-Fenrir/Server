using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.ContinueSkillStat,
    ExpectedSize = 73, AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct ContinueSkillStatRequest : IIncomingPacket<ContinueSkillStatRequest>
{
    [FixedArray(16)] public required int[] Skill { get; init; }
}
