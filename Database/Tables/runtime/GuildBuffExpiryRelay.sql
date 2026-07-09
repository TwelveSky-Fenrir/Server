-- Cross-shard fan-out outbox for the guild-buff-reserve-exhaustion immediate strip-effect push --
-- Fenrir's SQL-mediated stand-in for legacy's ts25center-driven BroadcastZone() cluster-wide send
-- (Server/ts25center/S07_MyGame01.cpp:291-331's edge-triggered gBuffTime<1 branch;
-- Server/ts25center/S05_MyTransfer.cpp:65-73/Server/ts25center/H05_MyTransfer.h:28-29's own dedicated wire
-- opcode), since Fenrir has no ts25center-equivalent process. Same "the originating shard delivers to its own
-- locally-hosted matching players synchronously and unchanged first" shape as
-- runtime.GuildTribeBroadcastRelay -- Hosting.Guilds.GuildBuffDecayHost posts directly onto this shard's own
-- ZoneRegistry before ever queuing a row here; GuildBuffExpiryRelayHost's own poll cycle then delivers to
-- every OTHER live shard's own locally-hosted matching players.
--
-- Same in-memory-OLTP, SCHEMA_ONLY shape as every other runtime.* table: a crash loses only in-flight,
-- not-yet-polled pushes -- the correct direction to fail for a best-effort session-state notification (the
-- guild's own persisted BuffTime, already floored to zero durably in game.Guilds by GuildBuffDecayHost, is
-- the source of truth every later GUILD_INFO read re-derives from regardless).
--
-- RelayId is an ever-increasing IDENTITY sequence every shard's own cursor (runtime.GuildBuffExpiryCursor) is
-- expressed against, not CreatedAtUtc -- see GuildTribeBroadcastRelay's own header for why wall-clock time is
-- not a safe "give me everything after X" predicate across concurrent inserts from different shard processes.
CREATE TABLE runtime.GuildBuffExpiryRelay
(
    RelayId       BIGINT IDENTITY (1,1) NOT NULL,
    SourceShardId TINYINT               NOT NULL, -- never re-delivered back to this shard; it already delivered locally
    GuildId       INT                   NOT NULL,
    NewBuffTime   INT                   NOT NULL, -- always < 1 (typically exactly zero, floored) whenever a row exists
    CreatedAtUtc  DATETIME2(3)          NOT NULL,
    CONSTRAINT PK_GuildBuffExpiryRelay PRIMARY KEY NONCLUSTERED (RelayId)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
