using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct GuildWorkBuffPayload : IFenrirWireType<GuildWorkBuffPayload>
{
    // 0-4; out of range aborts, matching the legacy's own Quit().
    public required int GuildBuffType { get; init; }
}
