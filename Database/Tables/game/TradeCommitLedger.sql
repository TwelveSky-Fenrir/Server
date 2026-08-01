-- C8 durable trade anti-dupe ledger. A DELIBERATE Fenrir uplift over the legacy in-memory swap, which offers
-- no crash/retry protection (Server/ts25zone/S04_MyWork02.cpp:8983-9008 performs the exchange purely in memory
-- and defers durability to the periodic avatar save). One row per COMMITTED confirmed trade, keyed by a
-- per-commit idempotency token generated once by the caller (TradeCommitToken.NewForCommit) and reused across
-- any retry of that same commit.
--
-- Purpose: usp_CharacterTradeCommit_ExecuteIdempotent claims the token (INSERT) as the very first write inside
-- its transaction. The UNIQUE index on TradeToken is the exactly-once backstop -- a second call carrying the
-- same token can never re-apply the ADDITIVE money deltas (the dupe vector the container DELETE+INSERT replaces
-- are naturally idempotent against, but Money = Money + @Delta is not). A benign retry hits the row's existence
-- fast-path and no-ops; a (TradeLockHandler-serialized, so unreachable-in-practice) concurrent duplicate loses
-- the INSERT race with 2627 and XACT_ABORT rolls its whole transaction back -- fail-safe, never a double grant.
--
-- No FK on CharacterA/CharacterB: those are diagnostic-only columns, and the token is inserted BEFORE the two
-- guarded money UPDATEs, so an unknown character must surface as the proc's own THROW 50268/50269 (matching
-- usp_CharacterTrade_Execute), never as a schema-level FK violation that would mask which leg failed. Same
-- reasoning as game.CharacterRunes' deliberate FK omission.
--
-- Retention: append-only; a future scheduled prune job may trim rows older than a chosen window. No runtime read
-- path depends on old rows -- only the presence/absence of the current commit's token matters.
CREATE TABLE game.TradeCommitLedger
(
    LedgerId       BIGINT           NOT NULL IDENTITY (1, 1),
    TradeToken     UNIQUEIDENTIFIER NOT NULL,
    CharacterA     INT              NOT NULL,
    CharacterB     INT              NOT NULL,
    CommittedAtUtc DATETIME2(3)     NOT NULL
        CONSTRAINT DF_TradeCommitLedger_CommittedAtUtc DEFAULT SYSUTCDATETIME(),
    -- Clustered on the monotonic identity for append-friendly insert locality (a GUID clustered key would
    -- fragment on every insert); the dedupe uniqueness lives on the nonclustered index below.
    CONSTRAINT PK_TradeCommitLedger PRIMARY KEY CLUSTERED (LedgerId),
    -- The exactly-once contract: a token may be committed at most once, ever.
    CONSTRAINT UQ_TradeCommitLedger_TradeToken UNIQUE NONCLUSTERED (TradeToken)
);
