using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(26)]
public readonly partial record struct GuildWorkTransferPayload : IFenrirWireType<GuildWorkTransferPayload>
{
    [FixedString(13)] public required string NewMasterName { get; init; }

    [FixedString(13)] public required string OldMasterName { get; init; }
}
