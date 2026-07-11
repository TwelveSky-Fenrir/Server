using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct GuildWorkBuffPayload : IFenrirWireType<GuildWorkBuffPayload>
{
    public required int GuildBuffType { get; init; }
}
