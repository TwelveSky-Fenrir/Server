-- database/50_procedures/game/usp_Character_SetAutoPotionThreshold.sql
-- Contract: persists the auto-potion HP/MP thresholds (CZ_CHANGE_AUTO_INFO, wAvatar.aAutoLifeRatio/
-- aAutoManaRatio, S04_MyWork02.cpp:11792) -- the handler is silent (no ZC reply), this proc is its only
-- durable effect.
-- Params: @CharacterId INT, @AutoLifeRatio TINYINT (0-5), @AutoManaRatio TINYINT (0-5).
-- Result set: none. Idempotent: yes.
CREATE PROCEDURE game.usp_Character_SetAutoPotionThreshold @CharacterId   INT,
    @AutoLifeRatio TINYINT,
    @AutoManaRatio TINYINT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

UPDATE game.Characters
SET AutoLifeRatio = @AutoLifeRatio,
    AutoManaRatio = @AutoManaRatio,
    UpdatedAtUtc  = SYSUTCDATETIME()
WHERE CharacterId = @CharacterId;
END;
