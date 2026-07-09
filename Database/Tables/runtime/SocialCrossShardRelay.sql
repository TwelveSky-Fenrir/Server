-- Cross-shard social-negotiation relay outbox (WS1.4 design: lightweight point-to-point relay for the six
-- character-to-character negotiation flows -- Party invite, Friend ask, Mentor ask, Duel ask, Trade invite,
-- Guild invite) -- Fenrir's SQL-mediated stand-in for the same missing ts25center relay uplink that
-- runtime.GuildTribeBroadcastRelay (this table's own sibling, see that table's header for the full
-- ts25center citation) already closes for the four cluster-wide broadcast opcodes. This table is modeled
-- byte-for-byte on that one's own shape, with one deliberate difference: GuildTribeBroadcastRelay is a
-- FAN-OUT (one publish, every OTHER shard's poll picks it up, filtered on SourceShardId <> @ShardId), while
-- this table is POINT-TO-POINT (one publish, only the ONE shard named in TargetShardId ever picks it up,
-- filtered on TargetShardId = @ShardId) -- a party/friend/mentor/duel/trade/guild-invite negotiation always
-- has exactly one intended recipient character, never a whole shard's population.
--
-- Same in-memory-OLTP, SCHEMA_ONLY shape as every other runtime.* relay table: a crash loses only
-- in-flight, not-yet-polled negotiation messages, the correct direction to fail for a best-effort social
-- prompt (the asker simply sees no response and can retry, exactly as if the target had not answered yet).
--
-- RelayId is an ever-increasing IDENTITY sequence, not CreatedAtUtc, for the same reason
-- GuildTribeBroadcastRelay's own header gives: wall-clock time is not monotonic enough across concurrent
-- inserts from different shard processes to safely drive a "give me everything after X" poll predicate.
-- Unlike the broadcast sibling, each shard's own runtime.SocialCrossShardRelayCursor position is expressed
-- against the subset of RelayId values where TargetShardId = that shard, not the whole global sequence --
-- see usp_SocialCrossShardRelay_Poll's own header for why that is still correct despite RelayId being a
-- single shared IDENTITY across every shard's rows.
--
-- MessageType distinguishes the two message shapes carried in this one table (Ask vs Answer) rather than
-- splitting them into two tables, since an Answer is always addressed back to the Ask's own SourceShardId/
-- SourceCharacterId and is otherwise structurally identical (same Kind, same source/target column pair,
-- just swapped) -- AskRelayId is the only field that gives an Answer row meaning, linking it back to the
-- RelayId of the Ask it answers so the original asker's shard can match its own held pending entry.
-- Accepted/ReasonCode are populated on Answer rows only (NULL on Ask rows): Accepted is the human or
-- auto-declined yes/no, ReasonCode is a small Fenrir-internal decline-reason enumeration (busy/stunned/dead/
-- level-gate/etc., owned by the *.Services layer that publishes it -- no legacy wire-opcode citation applies
-- since this whole relay is a Fenrir-only addition with no legacy analog).
--
-- SourceAvatarName is carried on every row (Ask AND Answer) so the receiving shard can render a prompt/
-- result without a second round trip back to the source shard for a display name; there is no
-- TargetAvatarName because the target shard always already knows its own local character's name from its
-- own ZoneRegistry.
CREATE TABLE runtime.SocialCrossShardRelay
(
    RelayId           BIGINT IDENTITY (1,1) NOT NULL,
    Kind              TINYINT               NOT NULL, -- 0 Party, 1 Friend, 2 Mentor, 3 Duel, 4 Trade, 5 GuildInvite
    MessageType       TINYINT               NOT NULL, -- 0 Ask, 1 Answer
    Accepted          BIT                   NULL,     -- Answer rows only
    ReasonCode        TINYINT               NULL,     -- Answer rows only (decline reason, Fenrir-internal)
    SourceShardId     TINYINT               NOT NULL,
    SourceCharacterId INT                   NOT NULL,
    SourceAvatarName  NVARCHAR(13)          NOT NULL,
    TargetShardId     TINYINT               NOT NULL,
    TargetCharacterId INT                   NOT NULL,
    AskRelayId        BIGINT                NULL,     -- Answer rows only: RelayId of the Ask being answered
    CreatedAtUtc      DATETIME2(3)          NOT NULL,
    CONSTRAINT PK_SocialCrossShardRelay PRIMARY KEY NONCLUSTERED (RelayId),
    INDEX IX_SocialCrossShardRelay_Target NONCLUSTERED (TargetShardId, RelayId)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
