-- Cross-shard fan-out outbox for the proxy/deputy-shop rental-extension consumables' best-effort
-- live-registry mirror update (world.Items 567/592/8422/8423 via CZ_USE_INVENTORY_ITEM_SEND). Single-purpose
-- sibling of runtime.GuildTribeBroadcastRelay -- modeled on that table's own fan-out shape (one publish,
-- every OTHER live shard's own poll picks it up, filtered on SourceShardId <> @ShardId), but with no Kind
-- column at all: unlike GuildTribeBroadcastRelay's four multiplexed broadcast opcodes, this table only ever
-- carries one message shape.
--
-- Legacy's own in-place live-registry update (Server/ts25zone/S04_MyWork03.cpp:2632-2675) only ever applied
-- on the single process holding a non-null pointer to the live shop registry (server/zone 37 under the
-- always-active PPSHOP_V2 build variant, Server/Header/Protocol/DEFINE.h:95), silently no-opping everywhere
-- else with no cross-process resynchronization path at all. This table is a deliberate hardening past that
-- legacy structural limitation (an explicit open decision left to the implementing engineer by the
-- originating behavior contract, not a reproduction of a legacy mechanism): whichever shard the acting
-- character is currently connected to publishes one row here after its own local, synchronous
-- Zone.TryUpdateProxyShopExpiration attempt (which only ever hits when that shard happens to also be hosting
-- zone 37's own Zone instance), and every OTHER live shard's own ProxyShopExpirationRelayHost poll applies
-- the same update to ITS locally-hosted zone-37 Zone instance, if any -- in practice only the one shard that
-- actually hosts zone 37 (Fenrir shards Zone instances disjointly by map, see ProxyShopZonePolicy's own
-- remarks) ever finds a match; every other shard's poll is a harmless no-op.
--
-- Same in-memory-OLTP, SCHEMA_ONLY shape as every other runtime.* relay table: a crash loses only in-flight,
-- not-yet-polled rows, the correct direction to fail for a best-effort mirror update -- the durable
-- game.OfflineShops.ShopDate write (IOfflineShopRepository.ExtendRentalAsync) that gates the client-visible
-- success/failure response has already committed before this row is even enqueued, so a lost row here never
-- desyncs anything the client can observe as a wrong answer, only delays the live/broadcasting copy catching
-- up.
--
-- RelayId is an ever-increasing IDENTITY sequence every shard's own cursor (runtime.ProxyShopExpirationRelayCursor)
-- is expressed against, not CreatedAtUtc -- same "wall-clock time is not monotonic enough across concurrent
-- inserts from different shard processes" reasoning as runtime.GuildTribeBroadcastRelay's own header.
--
-- CorrelationId is a client-generated idempotency token, same shape and purpose as
-- runtime.GuildTribeBroadcastRelay's own CorrelationId column -- see that table's header for the full
-- rationale (ProxyShopExpirationRelayEntry.CorrelationId is minted once per mirror update and stays stable
-- across CrossShardRelayRetry's retries of that same instance).
CREATE TABLE runtime.ProxyShopExpirationRelay
(
    RelayId           BIGINT IDENTITY (1,1) NOT NULL,
    SourceShardId     TINYINT               NOT NULL, -- never re-delivered back to this shard; it already attempted local delivery
    CharacterId       INT                   NOT NULL,
    NewExpirationDate INT                   NOT NULL, -- compact whole-number calendar date, GameDate.ToDayNumber shape
    CorrelationId     UNIQUEIDENTIFIER      NOT NULL,
    CreatedAtUtc      DATETIME2(3)          NOT NULL,
    CONSTRAINT PK_ProxyShopExpirationRelay PRIMARY KEY NONCLUSTERED (RelayId),
    CONSTRAINT UQ_ProxyShopExpirationRelay_CorrelationId UNIQUE NONCLUSTERED (CorrelationId),
    -- Supports usp_ProxyShopExpirationRelay_Poll's own time-based reap DELETE -- see
    -- GuildTribeBroadcastRelay's own index comment for the shared rationale.
    INDEX IX_ProxyShopExpirationRelay_CreatedAtUtc NONCLUSTERED (CreatedAtUtc)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
