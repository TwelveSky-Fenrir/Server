-- database/50_procedures/game/usp_GiftLog_GetByAccount.sql
-- Contract: @AccountId -> RS0 { GiftLogId, ProductId, Quantity, Value, Status, CreatedAtUtc }, one row per
--           historical state transition (Pending on enqueue, Claimed on claim) for that account's gifts,
--           newest-first.
-- Read-only, safe to retry. Served by IX_GiftLog_Account (AccountId).
-- Added as a fix during review: game.GiftLog was write-only (populated by usp_Gift_Enqueue/usp_Gift_Claim)
-- with no procedure able to read it back, which is a real gap under this schema's "no direct table/view
-- grants, procs-only" security model -- an audit trail nobody can query is not an audit trail.
CREATE PROCEDURE game.usp_GiftLog_GetByAccount @AccountId INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT GiftLogId, ProductId, Quantity, Value, Status, CreatedAtUtc
FROM game.GiftLog
WHERE AccountId = @AccountId
ORDER BY CreatedAtUtc DESC;
END;
