using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// Sort: 1=view counters, 2=claim (requires min level, aJoinWar>=1, aKillOtherTribe>=10, else Quit()); full inventory -> Result=3 without Quit().
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.DailyMission,
    ExpectedSize = 13, AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct DailyMissionRequest : IIncomingPacket<DailyMissionRequest>
{
    public required int Sort { get; init; }
}
