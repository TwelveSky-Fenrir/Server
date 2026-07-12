using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

public enum GuildTribeBroadcastKind : byte
{
    GuildAnnouncement = 0,
    GuildChat = 1,
    TribeAnnouncement = 2,
    TribeAnnouncementScroll = 3
}

public sealed record GuildTribeBroadcastRelayEntry(
    GuildTribeBroadcastKind Kind,
    byte SourceShardId,
    int? GuildId,
    byte? Tribe,
    byte RoleField,
    string AvatarName,
    string Content,
    bool HasItemLink,
    int? ItemLinkIndex,
    int? ItemLinkActivity,
    int? ItemLinkValue,
    int? ItemLinkSocket0,
    int? ItemLinkSocket1,
    int? ItemLinkSocket2)
{
    // Generated once, at construction, and never touched again -- CrossShardRelayRetry.RunAsync's retry
    // closure (Application/Fenrir.Application.Game.Hosting/CrossShardRelayRetry.cs) recreates nothing between
    // attempts, it just re-invokes relay.PublishAsync against this SAME entry instance, so every retry of one
    // logical publish carries the identical token while two genuinely distinct broadcasts never collide.
    // usp_GuildTribeBroadcastRelay_Publish uses this to skip a re-insert when a retry follows a lost
    // acknowledgement rather than a genuine failed commit, closing the duplicate-row gap an unconditional
    // retry would otherwise open for a user-visible chat/announcement message.
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}

// SourceShardId (present on GuildTribeBroadcastRelayEntry, the write-side row) is deliberately absent
// here -- not a gap. usp_GuildTribeBroadcastRelay_Poll.sql never projects it in its SELECT list; the
// column only backs that proc's own `WHERE SourceShardId <> @ShardId` fan-out-exclusion predicate (a
// shard must not re-deliver a message to itself, since it already delivered locally at publish time), it
// carries no information the polling shard needs to act on the message once excluded. Same shape on
// ProxyShopExpirationRelayDto/GuildBuffExpiryRelayDto/RvrSiegeEventRelayDto (verified against each proc's
// own Poll SELECT list) -- all four are pure fan-out relays. Contrast with SocialCrossShardRelayDto/
// ChatCrossShardWhisperDto, which stay point-to-point (one specific target shard, not "every other
// shard") and correctly DO keep SourceShardId, because the recipient's reply has to be routed back to the
// exact shard that asked. Before "fixing" a DTO that looks narrower than its Entry counterpart, check
// whether the missing column is WHERE-only in the corresponding Poll procedure first.
[GenerateDto]
public sealed partial record GuildTribeBroadcastRelayDto(
    long RelayId,
    byte Kind,
    int? GuildId,
    byte? Tribe,
    byte RoleField,
    string AvatarName,
    string Content,
    bool HasItemLink,
    int? ItemLinkIndex,
    int? ItemLinkActivity,
    int? ItemLinkValue,
    int? ItemLinkSocket0,
    int? ItemLinkSocket1,
    int? ItemLinkSocket2);
