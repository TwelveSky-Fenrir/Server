using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct GmMonsterInstanceIndexPayload : IFenrirWireType<GmMonsterInstanceIndexPayload>
{

        public required int MonsterIndex { get; init; }
}
