using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
///     Everything <c>ChatCrossShardRelayHost</c>'s outbound drain loop needs to call
///     <c>usp_ChatCrossShardRelay_Publish</c> for one cross-shard whisper (op39 / <c>SECRET_CHAT</c>) --
///     constructed by <c>WhisperService.ResolveAsync</c> after a same-shard <c>ZoneRegistry</c> miss resolves
///     the target on a DIFFERENT live shard via <c>ICharacterShardLocationRepository.FindByNameAsync</c>, and
///     enqueued through <see cref="IChatCrossShardRelayQueue" />. Point-to-point (every row already names its
///     one intended <see cref="TargetShardId" />/<see cref="TargetCharacterId" /> recipient), not fan-out --
///     the same shape as the social-negotiation relay's own <c>SocialCrossShardRelayEntry</c>, minus its
///     Ask/Answer negotiation: a whisper is fire-and-forget, there is no answer leg.
/// </summary>
/// <remarks>
///     The optional in-line item-link attachment the legacy whisper path can carry (conditional on the
///     <c>USE_ITEM_LINK_V2</c> macro, whose live/dead status in the shipped build is unverified) is NOT carried
///     across this relay -- the cross-shard leg delivers a zeroed link. The same-shard whisper path is
///     unaffected and still forwards the sender's link verbatim. See
///     <c>Database/Tables/runtime/ChatCrossShardRelay.sql</c>'s own header for why the link is deferred rather
///     than serialized speculatively.
/// </remarks>
public sealed record ChatCrossShardWhisperEntry(
    byte SourceShardId,
    int SourceCharacterId,
    string SourceAvatarName,
    byte TargetShardId,
    int TargetCharacterId,
    string TargetAvatarName,
    string Content,
    byte SenderAuthType);

// Ordinal-mapped: ctor order must match usp_ChatCrossShardRelay_Poll's SELECT order.
[GenerateDto]
public sealed partial record ChatCrossShardWhisperDto(
    long RelayId,
    byte SourceShardId,
    int SourceCharacterId,
    string SourceAvatarName,
    byte TargetShardId,
    int TargetCharacterId,
    string TargetAvatarName,
    string Content,
    byte SenderAuthType);
