-- Thin wrapper around world.vw_MonsterWithDropSummary, which self-labels itself a "Tooling/debugging view"
-- in its own header comment. Verified zero C# call sites (Infrastructure/, Application/, Tools/, Tests/) as
-- of the 2026-07-12 exhaustive dead-procedure sweep -- deliberate DBA-facing scaffolding for a future
-- admin/debug tool, not accidental dead code: distinct in purpose and read frequency from
-- world.usp_Monster_GetAll's boot-cache read path, which every real gameplay consumer uses instead. A
-- sibling "GetById" cluster in this schema (including usp_Monster_GetById) was deleted the same session for
-- being CRUD-completeness scaffolding with no such tooling justification -- this one was kept on purpose.
-- Do not delete for lacking a call site, and do not wire it into a gameplay read path -- if a genuine
-- boot-cache need for this shape ever appears, extend usp_Monster_GetAll instead of repurposing this proc.
CREATE OR ALTER PROCEDURE world.usp_Monster_GetDropSummary
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
