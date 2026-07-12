-- Added so game.GiftLog (write-only otherwise) can be queried under this schema's procs-only security model.
-- Re-evaluated in the 2026-07-12 dead-file-sweep-tables-procs pass: zero C# consumer today (GiftRepository.cs
-- only wraps GetPendingByAccount/ClaimIntoVault/Enqueue), but this is a provisioned-not-yet-consumed audit
-- read surface, not a superseded/duplicate primitive like usp_Gift_Claim (deleted the same pass) -- nothing
-- else replaces the capability this proc provides, so it was kept rather than deleted. Wire a real gift-claim
-- history feature to this proc rather than adding a second GetByAccount variant.
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
