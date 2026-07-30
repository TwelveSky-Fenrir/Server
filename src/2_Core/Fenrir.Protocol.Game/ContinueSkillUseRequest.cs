using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.ContinueSkillUse,
    ExpectedSize = 25, AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct ContinueSkillUseRequest : IIncomingPacket<ContinueSkillUseRequest>
{
    [FixedArray(3)] public required float[] Location { get; init; }
    public required int Sort { get; init; }
}
