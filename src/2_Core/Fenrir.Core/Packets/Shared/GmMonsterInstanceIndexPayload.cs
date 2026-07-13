using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct GmMonsterInstanceIndexPayload : IFenrirWireType<GmMonsterInstanceIndexPayload>
{
    public required int MonsterIndex { get; init; }
}
