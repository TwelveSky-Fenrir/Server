-- Cross-shard fan-out outbox for the party-membership resync-on-reconnect flow (legacy RELAY_LOGIN_PARTY_CHECK,
-- sort 108 -- Server/Header/Protocol/STRUCT.h:1093-1098: a sort code, a party name, an avatar name; built and
-- broadcast to the hub on a reconnecting member's first zone entry, Server/ts25zone/S04_MyWork02.cpp:1132-1140,
-- reconciled by the hub at case 108, Server/ts25center/S04_MyWork02.cpp:1443-1494). Fenrir's SQL-mediated
-- stand-in for that ts25zone<->ts25center leg, since Fenrir has no ts25center-equivalent process and its
-- PartyRegistry is per-shard in-memory only (Application/Fenrir.Application.Game.Domain/Extensions/
-- DomainServiceCollectionExtensions.cs:189's documented "cross-shard scope gap" -- this table is that gap's
-- transport half). Single-purpose fan-out sibling of the GuildTribeBroadcastRelay/Cursor pair (same shape,
-- no Kind column), see that table's own header for the shared in-memory-OLTP/SCHEMA_ONLY/IDENTITY-cursor
-- rationale.
--
-- FAN-OUT (poll excludes SourceShardId = @ShardId), not point-to-point: the legacy hub broadcasts its
-- reconciliation RESULT to EVERY connected zone, not a point reply to the requesting zone alone (case 108
-- above). A row published here is therefore delivered to every OTHER live shard's own PartyResyncRelayHost,
-- which routes it to whichever registered IPartyResyncRelayHandler owns the party reconciliation against that
-- shard's live PartyRegistry.
--
-- Sort carries the legacy relay sort so both the resync REQUEST (108, "does this claimed party still exist?")
-- and its RESULT legs (108 party-info roster resync / 109 party-break dissolve) can ride the same table --
-- the reconciling handler switches on it. PartyName is the party the reconnecting character claims to belong
-- to (its persisted party-name anchor, bounded by the 13-char name limit like every other avatar/party name
-- on the wire, Network/.../PartyRosterResponse.cs); AvatarName is the reconnecting/subject character.
--
-- Same "a crash loses only in-flight, not-yet-polled messages" SCHEMA_ONLY posture as every other runtime.*
-- relay table -- the correct direction to fail for a best-effort party resync (a missed resync manifests as
-- the reconnecting client's party UI staying/going empty, never as duped or lost durable state: no party
-- membership is persisted on this path at all -- the party-name anchor lives on the avatar record, and live
-- membership is in-process on each shard).
--
-- CorrelationId is a client-generated idempotency token, same shape and purpose as
-- runtime.GuildTribeBroadcastRelay's own CorrelationId column -- see that table's header for the full
-- rationale (PartyResyncRelayEntry.CorrelationId is minted once per row and stays stable across
-- CrossShardRelayRetry's retries of that same instance).
CREATE TABLE runtime.PartyResyncRelay
(
    RelayId           BIGINT IDENTITY (1,1) NOT NULL,
    Sort              TINYINT               NOT NULL, -- legacy relay sort: 108 request/party-info, 109 party-break
    SourceShardId     TINYINT               NOT NULL, -- never re-delivered back to this shard
    SourceCharacterId INT                   NOT NULL, -- the reconnecting/subject character
    PartyName         NVARCHAR(13)          NOT NULL,
    AvatarName        NVARCHAR(13)          NOT NULL,
    CorrelationId     UNIQUEIDENTIFIER      NOT NULL,
    CreatedAtUtc      DATETIME2(3)          NOT NULL,
    CONSTRAINT PK_PartyResyncRelay PRIMARY KEY NONCLUSTERED (RelayId),
    CONSTRAINT UQ_PartyResyncRelay_CorrelationId UNIQUE NONCLUSTERED (CorrelationId),
    -- Supports usp_PartyResyncRelay_Poll's own time-based reap DELETE -- see
    -- GuildTribeBroadcastRelay's own index comment for the shared rationale.
    INDEX IX_PartyResyncRelay_CreatedAtUtc NONCLUSTERED (CreatedAtUtc)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
