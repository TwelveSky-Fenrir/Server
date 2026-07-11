-- Cross-shard whisper (op39 / SECRET_CHAT) relay outbox: Fenrir's SQL-mediated stand-in for legacy's own
-- ts25zone<->ts25center whisper fan-out, for the ONE point-to-point private-chat opcode. Modeled
-- byte-for-byte on runtime.SocialCrossShardRelay (this table's own sibling, see that table's header for the
-- shared in-memory-OLTP/SCHEMA_ONLY/IDENTITY-cursor rationale), with two deliberate simplifications:
--   * No MessageType column -- a whisper is fire-and-forget (the legacy op39 path has no delivery
--     acknowledgement back to the sender at all: the sender is told "accepted, target located" the instant
--     the point-in-time lookup succeeds, before the relay leaves, and never learns whether delivery actually
--     happened). There is therefore no Ask/Answer negotiation to model the way SocialCrossShardRelay does.
--   * No Kind column -- whisper is the only chat opcode relayed point-to-point today. A future point-to-point
--     chat kind would add a Kind discriminator here the way SocialCrossShardRelay carries one.
--
-- POINT-TO-POINT (one publish, only the ONE shard named in TargetShardId ever picks it up, filtered on
-- TargetShardId = @ShardId), not fan-out: Fenrir resolves the target's current shard up-front via
-- runtime.CharacterShardLocation (the same global-session view legacy's ts25playuser held), so a whisper
-- always names its one intended recipient shard rather than being broadcast to every zone the way legacy's
-- sort-103 relay physically was. This is the deliberate, contract-permitted C# implementation shape for the
-- op39 relay (legacy fanned out and let each zone drop non-matching; Fenrir addresses the one shard directly
-- and drops if the target has since left -- the same silent best-effort clean-drop parity either way).
--
-- Same "a crash loses only in-flight, not-yet-polled messages" SCHEMA_ONLY posture as every other runtime.*
-- relay table -- the correct direction to fail for a best-effort private message (the sender already saw
-- "accepted"; an undelivered whisper simply vanishes, exactly as if the target had logged off between the
-- lookup and delivery).
--
-- The optional in-line item-link attachment the legacy whisper can carry (conditional on the USE_ITEM_LINK_V2
-- macro, whose live/dead status in the shipped ReleaseEU33 build is unverified this session) is NOT carried
-- here: the cross-shard leg delivers a zeroed link. Serializing a link across the relay is deferred rather
-- than invented speculatively for an unverified macro; the same-shard whisper path already forwards the link
-- verbatim and is unaffected. If the item-link carry turns out to be load-bearing, add
-- ItemLinkIndex/Activity/Value/Socket0..2 columns here the way runtime.GuildTribeBroadcastRelay carries them.
--
-- SourceAvatarName is carried on every row so the receiving shard can render the incoming whisper without a
-- second round trip for the sender's display name; TargetAvatarName is carried for logging and as a defensive
-- name cross-check, though delivery matches on TargetCharacterId (resolved from the directory), not name.
-- SenderAuthType is the sender's GM/premium auth flag, forwarded verbatim to the delivered ZC_SECRET_CHAT_RECV
-- (Result=3) so a GM whisper still renders with elevated status on the recipient's client.
CREATE TABLE runtime.ChatCrossShardRelay
(
    RelayId           BIGINT IDENTITY (1,1) NOT NULL,
    SourceShardId     TINYINT               NOT NULL,
    SourceCharacterId INT                   NOT NULL,
    SourceAvatarName  NVARCHAR(13)          NOT NULL,
    TargetShardId     TINYINT               NOT NULL,
    TargetCharacterId INT                   NOT NULL,
    TargetAvatarName  NVARCHAR(13)          NOT NULL,
    Content           NVARCHAR(61)          NOT NULL,
    SenderAuthType    TINYINT               NOT NULL,
    CreatedAtUtc      DATETIME2(3)          NOT NULL,
    CONSTRAINT PK_ChatCrossShardRelay PRIMARY KEY NONCLUSTERED (RelayId),
    INDEX IX_ChatCrossShardRelay_Target NONCLUSTERED (TargetShardId, RelayId)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
