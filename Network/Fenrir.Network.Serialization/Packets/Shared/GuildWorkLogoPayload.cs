using Fenrir.Network.Serialization.Attributes;

namespace Fenrir.Network.Serialization.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct GuildWorkLogoPayload : IFenrirWireType<GuildWorkLogoPayload>
{
    public required int Value { get; init; }
}
