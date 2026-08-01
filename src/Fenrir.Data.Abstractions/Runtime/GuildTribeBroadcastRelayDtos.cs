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
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}

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
