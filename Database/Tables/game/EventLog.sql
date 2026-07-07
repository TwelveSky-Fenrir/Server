-- Durable, append-only audit trail for domain-significant events (trade, currency movement, item
-- create/destroy, enchant/refine/socket rolls, GM actions, death, session lifecycle, account-security
-- incidents) -- the "no durable audit-log subsystem exists" gap. Two write paths land here:
-- usp_EventLog_Insert (single-row, synchronous, called standalone or as one more statement inside another
-- mutation's own transaction) and usp_EventLog_InsertBatch (TVP, for the high-frequency write-behind path --
-- see game.tvp_EventLogEntry and Fenrir.Data.WriteBehind.EventLogQueue). No FK constraints on
-- ActorAccountId/ActorCharacterId/TargetAccountId/TargetCharacterId: same reasoning as game.Gifts.ProductId/
-- game.CashLog.ProductId ("unenforced reference"), generalized -- a permanent audit record must survive the
-- deletion of the account/character it references, and must never block that deletion.
-- EventCode is an app-owned numbering scheme, not FK'd to a lookup table -- no legacy catalog of event codes
-- exists to seed one from (same "unenforced, caller-owned" posture as CashLog.Reason). Outcome is likewise a
-- caller/EventCode-defined code (success/failure/partial, etc.), not a fixed global enum here.
-- Category's CHECK is intentionally wider (0-63) than the 9 categories in use today (Trade=0/Currency=1/
-- ItemCreate=2/ItemDestroy=3/Enchant=4/GmAction=5/Death=6/Session=7/AccountSecurity=8, see
-- Fenrir.Data.Abstractions.Game.EventLogCategory) -- room for new categories without a schema migration,
-- while still catching an obviously-wrong value.
--
-- Indexing: deliberately NOT a clustered columnstore index. This table's write path includes single-row
-- inserts made INSIDE another mutation's own transaction (the whole point of usp_EventLog_Insert existing
-- as a callable sub-statement) -- columnstore trickle-insert targets bulk/analytics loads, not
-- one-row-per-call OLTP inserts sharing a transaction with a gameplay mutation. The stated read pattern
-- (time-range investigation queries: "this character's history", "today's GM actions") is also better
-- served by seek-friendly B-tree indexes than a columnstore full-segment scan. If a genuine aggregate/
-- dashboard analytics need shows up later, a NONCLUSTERED columnstore index is the additive fit (Microsoft's
-- own guidance: "OLTP workload that has some analytics queries") and can be added as a new script without
-- touching this one. Table partitioning (CreatedAtUtc sliding window) is also deferred, not implemented:
-- there is no partition-maintenance job infrastructure anywhere in Fenrir yet, and both Fenrir.Data.Tests'
-- and Fenrir.IntegrationTests' fixtures apply the manifest fresh into an ephemeral container per run, so a
-- boundary-seeding migration keyed off "today" has nothing meaningful to do in that environment. Revisit
-- once a real retention policy and a maintenance-job host exist.
CREATE TABLE game.EventLog
(
    EventLogId        BIGINT IDENTITY (1,1) NOT NULL,
    EventCode         SMALLINT              NOT NULL,
    Category          TINYINT               NOT NULL,
    ActorAccountId    INT                   NULL,
    ActorCharacterId  INT                   NULL,
    TargetAccountId   INT                   NULL,
    TargetCharacterId INT                   NULL,
    ShardId           SMALLINT              NULL,
    DeltaMoney        BIGINT                NULL,
    DeltaBigMoney     BIGINT                NULL,
    ItemId            INT                   NULL,
    Quantity          INT                   NULL,
    Outcome           TINYINT               NULL,
    Payload           NVARCHAR(MAX)         NULL,
    CreatedAtUtc      DATETIME2(3)          NOT NULL
        CONSTRAINT DF_EventLog_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_EventLog PRIMARY KEY CLUSTERED (EventLogId),
    CONSTRAINT CK_EventLog_Category CHECK (Category BETWEEN 0 AND 63),
    -- General time-range browse across every category (admin activity feed).
    INDEX IX_EventLog_CreatedAtUtc NONCLUSTERED (CreatedAtUtc) INCLUDE (EventCode, Category, Outcome, ActorAccountId, ActorCharacterId),
    -- Category-scoped investigation (e.g. "all GmAction events this week", "all AccountSecurity events today").
    INDEX IX_EventLog_Category_CreatedAtUtc NONCLUSTERED (Category, CreatedAtUtc) INCLUDE (EventCode, ActorAccountId, ActorCharacterId, TargetAccountId, TargetCharacterId, Outcome),
    -- Single most common support/investigation drilldown: "what happened to/by this character". Filtered --
    -- ActorCharacterId is null for account-only or system-triggered rows.
    INDEX IX_EventLog_ActorCharacterId NONCLUSTERED (ActorCharacterId, CreatedAtUtc) INCLUDE (Category, EventCode, DeltaMoney, ItemId, Quantity)
        WHERE ActorCharacterId IS NOT NULL
);
