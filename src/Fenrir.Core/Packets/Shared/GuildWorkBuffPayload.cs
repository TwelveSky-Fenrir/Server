using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct GuildWorkBuffPayload : IFenrirWireType<GuildWorkBuffPayload>
{
    public required int GuildBuffType { get; init; }
}
