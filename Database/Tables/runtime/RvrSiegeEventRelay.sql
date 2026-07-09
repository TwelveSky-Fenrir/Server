-- Cross-shard fan-out outbox for the rvr-siege world-event family riding the legacy's op33
-- ZCP_ZONE_BROADCAST_FOR_CENTER_SEND / op94 ZC_BROADCAST_INFO_RECV pair: the Zone049 siege-zone-slot
-- sub-codes (1-9, ZoneCenterBroadcastIngestor) and the tribe-symbol/alliance tSort family
-- (38/39/40/42/45/46/47, ZoneEventBroadcaster). Single-purpose sibling of runtime.GuildTribeBroadcastRelay --
-- modeled on that table's own fan-out shape (one publish, every OTHER live shard's own poll picks it up,
-- filtered on SourceShardId <> @ShardId), but with no Kind column: every sort this relay ever carries already
-- funnels through the SAME generic 130-byte ZoneEventInfoResponse.Data payload shape on the wire itself
-- (Server/Header/Protocol/ZONE.h:19-24, Server/Header/Protocol/DEFINE.h:377-381), so a single opaque Sort+Data
-- pair carries every one of them without a per-field breakdown.
--
-- Legacy's own single ts25center process relayed to literally every connected ts25zone process
-- (Server/ts25center/S04_MyWork02.cpp:1213-1221) -- Fenrir has no ts25center-equivalent process and shards
-- ZoneRegistry disjointly by map, so the originating shard's own ZoneCenterBroadcastIngestor.Ingest/
-- ZoneEventBroadcaster.Announce* methods deliver to their own locally-hosted maps/players synchronously and
-- unchanged, then enqueue one row here so every OTHER live shard's own RvrSiegeEventRelayHost replays the same
-- (sort, data) pair -- via ApplyRelayedEvent -- to ITS own locally-hosted maps/players on its own next poll.
--
-- Same in-memory-OLTP, SCHEMA_ONLY shape as every other runtime.* relay table: a crash loses only in-flight,
-- not-yet-polled rows -- the correct direction to fail, since Zone049/tribe-symbol/alliance state that
-- genuinely needs to survive a restart already lives in game.WorldState/WorldStateTribes/WorldStateAllianceOffers
-- (see WorldStateService's own remarks); this table is purely the real-time propagation leg.
--
-- RelayId is an ever-increasing IDENTITY sequence every shard's own cursor (runtime.RvrSiegeEventRelayCursor)
-- is expressed against, not CreatedAtUtc -- same "wall-clock time is not monotonic enough across concurrent
-- inserts from different shard processes" reasoning as runtime.GuildTribeBroadcastRelay's own header.
CREATE TABLE runtime.RvrSiegeEventRelay
(
    RelayId       BIGINT IDENTITY (1,1) NOT NULL,
    SourceShardId TINYINT               NOT NULL, -- never re-delivered back to this shard; it already delivered locally
    Sort          INT                   NOT NULL, -- ZoneEventInfoResponse.Sort: Zone049 sub-code (1-9) or tSort (38/39/40/42/45/46/47)
    Data          VARBINARY(130)        NOT NULL, -- ZoneEventInfoResponse.Data, byte-for-byte
    CreatedAtUtc  DATETIME2(3)          NOT NULL,
    CONSTRAINT PK_RvrSiegeEventRelay PRIMARY KEY NONCLUSTERED (RelayId)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
