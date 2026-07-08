using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct GuildWorkLogoPayload : IFenrirWireType<GuildWorkLogoPayload>
{
    public required int Value { get; init; }
}
