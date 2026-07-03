-- database/50_procedures/game/usp_WorldState_Update.sql
-- Contract: update the singleton row's own contested-zone/monster-symbol/high-tribe/point-recompute fields
-- (see game.WorldState for what each column means and why it lives on the singleton, not a child table).
-- Params:
--   @Zone038WinTribe      TINYINT NULL
--   @Zone038WinTribeTime  INT     NULL
--   @TribeSymbolBattle    BIT
--   @MonsterSymbol        TINYINT NULL
--   @MonsterSymbolEndTime INT     NULL
--   @HighTribe            TINYINT NULL
--   @UpdateTribePoint     SMALLINT
-- Result set: none.
-- Idempotent: yes -- a no-op in effect if called twice with the same values (UpdatedAtUtc still refreshes).
-- A missing singleton row (game.usp_WorldState_EnsureInitialized never called) means this affects 0 rows;
-- the caller cannot distinguish that from "already had these exact values" (by design, matching
-- game.usp_Character_Delete's own idempotent-no-error idiom).
CREATE PROCEDURE game.usp_WorldState_Update @Zone038WinTribe      TINYINT = NULL,
    @Zone038WinTribeTime  INT = NULL,
    @TribeSymbolBattle    BIT,
    @MonsterSymbol        TINYINT = NULL,
    @MonsterSymbolEndTime INT = NULL,
    @HighTribe            TINYINT = NULL,
    @UpdateTribePoint     SMALLINT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

UPDATE game.WorldState
SET Zone038WinTribe      = @Zone038WinTribe,
    Zone038WinTribeTime  = @Zone038WinTribeTime,
    TribeSymbolBattle    = @TribeSymbolBattle,
    MonsterSymbol        = @MonsterSymbol,
    MonsterSymbolEndTime = @MonsterSymbolEndTime,
    HighTribe            = @HighTribe,
    UpdateTribePoint     = @UpdateTribePoint,
    UpdatedAtUtc         = SYSUTCDATETIME()
WHERE Id = 1;
END;
