using Fenrir.Network.Serialization.Attributes;

namespace Fenrir.Network.Serialization.Packets.Shared;

[FenrirWireType(4)]
public readonly record struct GuildWorkBuffPayload : IFenrirWireType<GuildWorkBuffPayload>
{
    // 0-4; out of range aborts, matching the legacy's own Quit().
    public required int GuildBuffType { get; init; }
}
