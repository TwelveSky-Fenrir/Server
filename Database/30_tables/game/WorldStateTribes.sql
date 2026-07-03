-- Normalizes the genuinely per-tribe (MAX_TRIBE_NUM=4) sub-parts of legacy `worldinfo`'s WORLD_INFO-backed
-- columns into one row per tribe, instead of the wide mTribeSymbolDateN/mTribeSymbolN/mTribePointN/
-- mTribeCloseInfoN column-per-tribe shape the raw dump uses.
-- HasSymbol -- Protocol/STRUCT.h's WORLD_INFO declares `int mTribeSymbol[MAX_TRIBE_NUM]`, and the dump's
-- own defaults (mTribeSymbol0=0, mTribeSymbol1=1, mTribeSymbol2=2, mTribeSymbol3=3) show this is actually an
-- int holding WHICH TRIBE currently possesses tribe N's symbol (identity-mapped = "no one has captured it
-- yet"), not a boolean. This table simplifies that down to a BIT ("does this tribe still hold its own
-- symbol") per this pass's target shape; which OTHER tribe captured it (if any) is not tracked. Defaults to
-- 1 (true), matching the legacy identity-mapped starting state.
-- IsClosed -- legacy `mTribeCloseInfo[2]` is only a 2-element array, not MAX_TRIBE_NUM(4) wide, so it does
-- not cleanly map one flag per tribe the way mTribeSymbol/mTribePoint do. Kept as one IsClosed BIT per tribe
-- anyway (widened/normalized superset, defaulting to 0/false for every tribe) per this table's brief-given
-- shape -- tribes 2/3 simply have no legacy source column to seed from, which is moot since this table
-- starts empty regardless.
-- Points: reconciled post-merge -- an earlier pass had folded legacy `mTribePoint[MAX_TRIBE_NUM]` into
-- game.Tribes itself too (that table no longer has a Points column; see its own header). This table is
-- the single source of truth for it now, since the legacy field's real home is WORLD_INFO (world/global
-- state), not TRIBE_INFO. game.usp_Tribe_GetAll exposes it via a LEFT JOIN for callers that want both.
-- SymbolDate has no WORLD_INFO struct backing at all (only mTribeSymbolDateN exists in the MySQL dump, not
-- the runtime struct) -- kept as a DB-only audit column ("when did this tribe's symbol state last change").
-- PLAIN (not memory-optimized) table: needs an enforced FOREIGN KEY to game.Tribes (regular disk-based --
-- see game.HeroRankings for the same In-Memory OLTP constraint), and tribe symbol/point/close-state changes
-- are not a per-tick hot path.
-- Starts empty (no existing players yet) -- schema only, no seed. The 4 rows (TribeId 0-3) are created by
-- game.usp_WorldState_EnsureInitialized alongside the game.WorldState singleton row.
CREATE TABLE game.WorldStateTribes
(
    TribeId    TINYINT NOT NULL,
    SymbolDate DATETIME2(3) NULL,
    HasSymbol  BIT     NOT NULL CONSTRAINT DF_WorldStateTribes_HasSymbol DEFAULT 1,
    Points     INT     NOT NULL CONSTRAINT DF_WorldStateTribes_Points DEFAULT 0,
    IsClosed   BIT     NOT NULL CONSTRAINT DF_WorldStateTribes_IsClosed DEFAULT 0,
    CONSTRAINT PK_WorldStateTribes PRIMARY KEY CLUSTERED (TribeId),
    CONSTRAINT FK_WorldStateTribes_Tribe FOREIGN KEY (TribeId) REFERENCES game.Tribes (TribeId)
);
