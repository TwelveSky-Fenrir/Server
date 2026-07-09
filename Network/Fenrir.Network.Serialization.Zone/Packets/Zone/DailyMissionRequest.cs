using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

// Sort: 1=view counters, 2=claim (requires min level, aJoinWar>=1, aKillOtherTribe>=10, else Quit()); full inventory -> Result=3 without Quit().
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.DailyMission,
    ExpectedSize = 13, AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly record struct DailyMissionRequest : IIncomingPacket<DailyMissionRequest>
{
    public required int Sort { get; init; }
}
