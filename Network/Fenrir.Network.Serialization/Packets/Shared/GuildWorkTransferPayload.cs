using Fenrir.Network.Serialization.Attributes;

namespace Fenrir.Network.Serialization.Packets.Shared;

[FenrirWireType(26)]
public readonly record struct GuildWorkTransferPayload : IFenrirWireType<GuildWorkTransferPayload>
{
    [FixedString(13)] public required string NewMasterName { get; init; }

    // Legacy overwrites this with the zone's own avatar name; callers should use the requester's
    // own name instead, never this field.
    [FixedString(13)] public required string OldMasterName { get; init; }
}
