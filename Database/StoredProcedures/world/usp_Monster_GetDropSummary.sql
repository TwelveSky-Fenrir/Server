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
