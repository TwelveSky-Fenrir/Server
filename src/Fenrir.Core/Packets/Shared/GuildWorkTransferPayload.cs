using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(26)]
public readonly partial record struct GuildWorkTransferPayload : IFenrirWireType<GuildWorkTransferPayload>
{
    [FixedString(13)] public required string NewMasterName { get; init; }

    [FixedString(13)] public required string OldMasterName { get; init; }
}
