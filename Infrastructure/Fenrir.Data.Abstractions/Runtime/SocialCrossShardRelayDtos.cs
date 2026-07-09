using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
///     Which of the six character-to-character negotiation flows a <c>runtime.SocialCrossShardRelay</c> row
///     represents -- Party invite, Friend ask, Mentor ask, Duel ask, Trade invite, Guild invite. Numeric
///     values are Fenrir-internal (this table's own <c>Kind</c> column), not legacy wire opcodes.
/// </summary>
public enum SocialCrossShardRelayKind : byte
{
    Party = 0,
    Friend = 1,
    Mentor = 2,
    Duel = 3,
    Trade = 4,
    GuildInvite = 5
}

/// <summary>
///     Whether a <c>runtime.SocialCrossShardRelay</c> row is the initial prompt (Ask, published by the
///     asker's own shard after a same-shard <c>ZoneRegistry</c> miss resolves the target cross-shard) or the
///     response to one (Answer, published by the target's own shard once it resolves and re-runs its own
///     target-side pre-checks -- see <see cref="AskRelayId" /> for how an Answer links back to its Ask).
/// </summary>
public enum SocialCrossShardRelayMessageType : byte
{
    Ask = 0,
    Answer = 1
}

/// <summary>
///     Everything a future <c>SocialCrossShardRelayHost</c>'s outbound drain loop needs to call
///     <c>usp_SocialCrossShardRelay_Publish</c> -- constructed by the six negotiation *.Services call sites
///     (<c>PartyInviteService.Invite</c>, <c>FriendService.Ask</c>, <c>MentorAskService.Ask</c>,
///     <c>DuelService.Ask</c>, <c>TradeInviteService.Invite</c>, <c>GuildInviteService.Ask</c>) after a
///     same-shard scan miss resolves the target via <c>ICharacterShardLocationRepository.FindByNameAsync</c>,
///     and by whichever shard publishes an Answer back to the original asker's shard.
///     <see cref="Accepted" />/<see cref="ReasonCode" /> are populated on Answer rows only (null on Ask
///     rows); <see cref="AskRelayId" /> is populated on Answer rows only, carrying the RelayId of the Ask
///     being answered so the original asker's shard can match it against its own held pending entry -- see
///     <c>Database/Tables/runtime/SocialCrossShardRelay.sql</c>'s own header for the full design.
/// </summary>
public sealed record SocialCrossShardRelayEntry(
    SocialCrossShardRelayKind Kind,
    SocialCrossShardRelayMessageType MessageType,
    bool? Accepted,
    byte? ReasonCode,
    byte SourceShardId,
    int SourceCharacterId,
    string SourceAvatarName,
    byte TargetShardId,
    int TargetCharacterId,
    long? AskRelayId);

// Ordinal-mapped: ctor order must match usp_SocialCrossShardRelay_Poll's SELECT order.
[GenerateDto]
public sealed partial record SocialCrossShardRelayDto(
    long RelayId,
    byte Kind,
    byte MessageType,
    bool? Accepted,
    byte? ReasonCode,
    byte SourceShardId,
    int SourceCharacterId,
    string SourceAvatarName,
    byte TargetShardId,
    int TargetCharacterId,
    long? AskRelayId);
