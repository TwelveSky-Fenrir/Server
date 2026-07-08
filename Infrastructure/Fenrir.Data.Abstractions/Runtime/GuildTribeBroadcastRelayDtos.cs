using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
///     Which of the four cluster-wide guild/tribe broadcast opcodes a <c>runtime.GuildTribeBroadcastRelay</c>
///     row represents -- GuildAnnouncement (legacy opcode 76), GuildChat (77), TribeAnnouncement (80),
///     TribeAnnouncementScroll (112). Numeric values are Fenrir-internal (this table's own <c>Kind</c>
///     column), not legacy wire opcodes or relay sorts.
/// </summary>
public enum GuildTribeBroadcastKind : byte
{
    GuildAnnouncement = 0,
    GuildChat = 1,
    TribeAnnouncement = 2,
    TribeAnnouncementScroll = 3
}

/// <summary>
///     Everything <c>GuildTribeBroadcastRelayHost</c>'s outbound drain loop needs to call
///     <c>usp_GuildTribeBroadcastRelay_Publish</c> -- see <see cref="IGuildTribeBroadcastRelayQueue" /> for
///     where this is constructed (the four cluster-wide broadcast *.Services call sites, immediately after
///     each one's own unchanged, synchronous same-shard delivery) and enqueued.
///     <see cref="RoleField" />/<see cref="ItemLinkIndex" />/<see cref="ItemLinkActivity" />/
///     <see cref="ItemLinkValue" />/<see cref="ItemLinkSocket0" />/<see cref="ItemLinkSocket1" />/
///     <see cref="ItemLinkSocket2" /> are Kind-dependent -- see
///     <c>Database/Tables/runtime/GuildTribeBroadcastRelay.sql</c>'s own header for the full mapping.
/// </summary>
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
    int? ItemLinkSocket2);

// Ordinal-mapped: ctor order must match usp_GuildTribeBroadcastRelay_Poll's SELECT order.
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
