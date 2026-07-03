using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

[FenrirWireType(16)]
public readonly partial record struct MissionDate : IFenrirWireType<MissionDate>
{
    public required int JoinWar { get; init; }
    public required int KillOtherTribe { get; init; }
    public required int KillMonster { get; init; }
    public required int PlayTime { get; init; }
}
