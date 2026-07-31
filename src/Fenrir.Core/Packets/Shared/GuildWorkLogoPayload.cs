using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct GuildWorkLogoPayload : IFenrirWireType<GuildWorkLogoPayload>
{
    public required int Value { get; init; }
}
