using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// Location is a dead field: never read by the C++ handler, still part of the wire layout. Sort: 1=start
// channeling (mana-gated, broadcasts the resulting action), 2=per-tick buff application (no reply); else Quit().
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.ContinueSkillUse,
    ExpectedSize = 25, AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct ContinueSkillUseRequest : IIncomingPacket<ContinueSkillUseRequest>
{
    [FixedArray(3)] public required float[] Location { get; init; }
    public required int Sort { get; init; }
}
