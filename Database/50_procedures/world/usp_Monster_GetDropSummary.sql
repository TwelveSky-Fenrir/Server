-- Contract: no parameters -> RS0, one row per monster (1139 rows): { MonsterId INT, Name NVARCHAR(25),
-- RealLevel SMALLINT, Life INT, MoneyDropRate INT NULL, MoneyMinAmount INT NULL, MoneyMaxAmount INT NULL,
-- PotionSlotCount INT, ExtraItemSlotCount INT, CategoryRateSlotCount INT, HasQuestItemDrop BIT }.
-- Tooling/debugging only (loot-table richness at a glance) -- thin wrapper so world.vw_MonsterWithDropSummary
-- stays an internal-to-the-schema readability tool per the security model (app code/tools never SELECT
-- a view directly, only EXECUTE a procedure).
-- Idempotent: yes (read-only). Errors: none.
CREATE PROCEDURE world.usp_Monster_GetDropSummary
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT MonsterId,
       Name,
       RealLevel,
       Life,
       MoneyDropRate,
       MoneyMinAmount,
       MoneyMaxAmount,
       PotionSlotCount,
       ExtraItemSlotCount,
       CategoryRateSlotCount,
       HasQuestItemDrop
FROM world.vw_MonsterWithDropSummary
ORDER BY MonsterId;
END;
