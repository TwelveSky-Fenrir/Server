using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(16)]
public readonly record struct MissionDate : IFenrirWireType<MissionDate>
{
    public required int JoinWar { get; init; }
    public required int KillOtherTribe { get; init; }
    public required int KillMonster { get; init; }
    public required int PlayTime { get; init; }
}
