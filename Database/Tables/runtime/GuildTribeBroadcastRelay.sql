-- Cross-shard fan-out outbox for the four cluster-wide guild/tribe broadcast opcodes (GuildAnnouncement 76,
-- GuildChat 77, TribeAnnouncement 80, TribeAnnouncementScroll 112) -- Fenrir's SQL-mediated stand-in for
-- legacy's ts25zone<->ts25center relay uplink (opcodes 55-57 riding the same persistent connection,
-- Server/ts25zone/H06_MyUpperCom.h:380, Server/Header/Protocol/RELAY.h:6-29), since Fenrir has no
-- ts25center-equivalent process (see WhisperService/IWorldNoticeService's own remarks for the same
-- previously-accepted limitation; closed for these four opcodes only). The originating shard delivers to its
-- own locally-hosted matching players synchronously and unchanged (GuildAnnouncementService et al. still
-- iterate this process's own ZoneRegistry first, exactly as before); it then queues one row here so every
-- OTHER live shard's own GuildTribeBroadcastRelayHost delivers to ITS locally-hosted matching players on its
-- own next poll -- see that host's own remarks for the publish/poll/cursor/reap mechanics.
--
-- Same in-memory-OLTP, SCHEMA_ONLY shape as every other runtime.* table: a crash loses only in-flight,
-- not-yet-polled broadcasts, the correct direction to fail for best-effort chat/announcement delivery (no
-- legacy parity requirement demands durable cross-restart delivery -- ts25center itself is a live in-memory
-- relay, not a durable queue, per the Cluster Overview's own "the relay is not a separate connection" note).
--
-- RelayId is an ever-increasing IDENTITY sequence every shard's own cursor (runtime.GuildTribeBroadcastCursor)
-- is expressed against, not CreatedAtUtc -- wall-clock time is not monotonic enough across concurrent inserts
-- from different shard processes to safely drive a "give me everything after X" poll predicate.
--
-- ItemLink* columns are GuildChat (Kind=1) only, flattened rather than a VARBINARY blob to match this
-- repository family's own typed-scalar-parameter convention (CaeriusNet's SqlDbType-typed AddParameter, no
-- inline blobs) -- see Network/Fenrir.Network.Serialization/Packets/Shared/ItemLinkInfo.cs for the four
-- underlying int fields (Index/Activity/Value/Socket[3]) this mirrors.
--
-- RoleField is Kind-dependent: TribeAnnouncement (Kind=2) carries the sender's real 1/2/3 tribe role;
-- TribeAnnouncementScroll (Kind=3) carries the sender's raw tribe number in this same field position (the
-- wire-field quirk TribeAnnouncementScrollResponse.TribeRole's own docstring already flags -- ProcessForRelay
-- substitutes the tribe id here, not the sender's hardcoded placeholder, Server/ts25zone/S04_MyWork04.cpp:
-- 265-286); unused (0) for GuildAnnouncement/GuildChat (Kind 0/1).
--
-- CorrelationId is a client-generated idempotency token (GuildTribeBroadcastRelayEntry.CorrelationId, minted
-- once when the entry is constructed and stable across every retry attempt CrossShardRelayRetry.RunAsync
-- makes for that SAME entry instance). usp_GuildTribeBroadcastRelay_Publish looks this up before inserting so
-- a retry after a lost acknowledgement (the commit actually succeeded, only the ack didn't arrive) finds its
-- own already-committed row and skips the re-insert, instead of duplicating a user-visible chat/announcement
-- message. The UNIQUE constraint is the backstop against a genuine concurrent double-publish of the same
-- token racing the lookup itself, not the primary dedup mechanism.
CREATE TABLE runtime.GuildTribeBroadcastRelay
(
    RelayId          BIGINT IDENTITY (1,1) NOT NULL,
    Kind             TINYINT               NOT NULL, -- 0 GuildAnnouncement, 1 GuildChat, 2 TribeAnnouncement, 3 TribeAnnouncementScroll
    SourceShardId    TINYINT               NOT NULL, -- never re-delivered back to this shard; it already delivered locally
    GuildId          INT                   NULL,     -- Kind 0/1 only
    Tribe            TINYINT               NULL,     -- Kind 2/3 only
    RoleField        TINYINT               NOT NULL, -- see header comment
    AvatarName       NVARCHAR(13)          NOT NULL,
    Content          NVARCHAR(61)          NOT NULL,
    HasItemLink      BIT                   NOT NULL,
    ItemLinkIndex    INT                   NULL,
    ItemLinkActivity INT                   NULL,
    ItemLinkValue    INT                   NULL,
    ItemLinkSocket0  INT                   NULL,
    ItemLinkSocket1  INT                   NULL,
    ItemLinkSocket2  INT                   NULL,
    CorrelationId    UNIQUEIDENTIFIER      NOT NULL,
    CreatedAtUtc     DATETIME2(3)          NOT NULL,
    CONSTRAINT PK_GuildTribeBroadcastRelay PRIMARY KEY NONCLUSTERED (RelayId),
    CONSTRAINT UQ_GuildTribeBroadcastRelay_CorrelationId UNIQUE NONCLUSTERED (CorrelationId),
    -- Supports usp_GuildTribeBroadcastRelay_Poll's own time-based reap DELETE (WHERE CreatedAtUtc <=
    -- retention cutoff), issued once per shard per poll cycle (every 2s by default, N shards) -- without
    -- this, that DELETE has no usable predicate index and falls back to a full index-order scan on every
    -- single poll. CreatedAtUtc is write-once at INSERT and never updated afterward, so this index costs
    -- nothing on the hot insert/publish path, unlike an index on a column that gets touched repeatedly.
    INDEX IX_GuildTribeBroadcastRelay_CreatedAtUtc NONCLUSTERED (CreatedAtUtc)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
