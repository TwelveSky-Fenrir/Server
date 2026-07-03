-- Singleton row for legacy `worldinfo` (nxtserver.sql, PK wIndex, always exactly one row in the dump).
-- Id/CK_WorldState_Id enforce the singleton the same way a well-known "config row" table would elsewhere.
-- Direct inspection of Protocol/STRUCT.h's WORLD_INFO struct confirms worldinfo only persists a small
-- subset of the much larger runtime struct (which also tracks dozens of zone-event/guild-battle/war-point
-- fields never written to MySQL) -- this table mirrors exactly what the dump actually persists, not the
-- full in-memory struct.
-- Kept as singleton scalar columns (genuinely singular, confirmed against WORLD_INFO, not per-tribe arrays):
--   Zone038WinTribe/Zone038WinTribeTime -- mZone038WinTribe/mZone038WinTribeTime: which tribe currently
--     holds zone 38 and since when. Legacy default -1 for Zone038WinTribe is an explicit "no winner yet"
--     sentinel distinct from every real TribeId (0-3), so -1 -> NULL (same translate-sentinel-to-NULL
--     convention used everywhere else in this schema, just -1 here instead of 0 since 0 is itself a valid
--     TribeId in this domain).
--   TribeSymbolBattle -- mTribeSymbolBattle: a lone int scalar (not a MAX_TRIBE_NUM array like its
--     neighbours), so it stays here rather than moving to game.WorldStateTribes. Modeled as BIT (observed
--     legacy default 0, name strongly suggests an active/inactive flag); the legacy field is a full int
--     whose broader value range (if any) beyond active/inactive is not captured by this simplification.
--   MonsterSymbol/MonsterSymbolEndTime -- mMonsterSymbol/mMonsterSymbolEndTime: which tribe currently holds
--     the monster-symbol world buff and until when. Same TribeId semantics as Zone038WinTribe, but the
--     legacy default here is 3 (a real TribeId, not a -1 sentinel) -- kept NULLable for symmetry/future
--     "unclaimed" states, no 0/-1 translation applied since nothing in the dump signals "unset" for this one.
--   HighTribe -- mHighTribe: legacy default is a plain 0, which is BOTH a valid TribeId AND the field's
--     resting default -- unlike Zone038WinTribe/mAllianceState's unambiguous -1 sentinel, there is no way
--     to tell "Tribe 0 leads" from "not computed yet" from the dump alone, so no 0->NULL translation is
--     applied here; the column simply allows NULL for the application layer's own "not computed" state.
--   UpdateTribePoint -- UpdateTribePoint: a small dirty-flag/recompute counter, not a tribe reference.
-- mHighTribe/UpdateTribePoint/mClose4VoteState/mTribe4Quest* are NOT part of the WORLD_INFO struct at all
-- (confirmed by direct inspection) -- i.e. they are MySQL-only bookkeeping columns with no shared-memory
-- backing. mTribeVoteState[4]/mCloseVoteState[4]/mClose4VoteState/mTribe4QuestDate/State/Name are tribe-
-- internal voting/quest mechanics, not world/contested-zone state -- deliberately NOT modeled here; that is
-- the guilds-tribes domain's concern (game.Tribes/game.TribeVotes), not this domain's WorldState.
-- Normalized out to child tables (see game.WorldStateTribes / game.WorldStateAllianceOffers): the genuinely
-- per-tribe (mTribeSymbol[4]/mTribePoint[4]/mTribeCloseInfo[2]) and matrix-shaped
-- (mPossibleAllianceInfo[4][2]) sub-parts.
-- Starts empty (no existing players yet) -- schema only, no seed. The single row (Id=1) is created by
-- game.usp_WorldState_EnsureInitialized, an idempotent bootstrap the application calls once at world
-- startup, since GameServer can only ever reach this schema through a procedure.
CREATE TABLE game.WorldState
(
    Id                   TINYINT  NOT NULL CONSTRAINT DF_WorldState_Id DEFAULT 1,
    Zone038WinTribe      TINYINT NULL,
    Zone038WinTribeTime  INT NULL,
    TribeSymbolBattle    BIT      NOT NULL CONSTRAINT DF_WorldState_TribeSymbolBattle DEFAULT 0,
    MonsterSymbol        TINYINT NULL,
    MonsterSymbolEndTime INT NULL,
    HighTribe            TINYINT NULL,
    UpdateTribePoint     SMALLINT NOT NULL CONSTRAINT DF_WorldState_UpdateTribePoint DEFAULT 0,
    UpdatedAtUtc         DATETIME2(3) NOT NULL CONSTRAINT DF_WorldState_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_WorldState PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT CK_WorldState_Id CHECK (Id = 1),
    CONSTRAINT FK_WorldState_Zone038WinTribe FOREIGN KEY (Zone038WinTribe) REFERENCES game.Tribes (TribeId),
    CONSTRAINT FK_WorldState_MonsterSymbol FOREIGN KEY (MonsterSymbol) REFERENCES game.Tribes (TribeId),
    CONSTRAINT FK_WorldState_HighTribe FOREIGN KEY (HighTribe) REFERENCES game.Tribes (TribeId)
);
