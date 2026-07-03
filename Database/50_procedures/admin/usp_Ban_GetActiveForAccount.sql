-- Contract: @AccountId INT -> RS0, 0..n rows (usually 0 or 1, but bans are a log so more than one
-- historical row could theoretically overlap -- caller treats "any row returned" as banned).
-- Called on the login path (LoginServer, right after/alongside auth.usp_Account_Authenticate) to catch
-- an active ban that predates or overrides auth.Accounts.IsBanned. Read-only, idempotent, no errors.
-- Plain table/proc, not native: admin.Bans cannot be memory-optimized (see its table comment -- it
-- needs real FKs to disk-based auth.Accounts/game.Characters), so this already runs as a b-tree index
-- seek on IX_Bans_Account, which is more than adequate for M1's scale.
CREATE PROCEDURE admin.usp_Ban_GetActiveForAccount @AccountId INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT BanId,
       AccountId,
       AccountLoginName,
       CharacterId,
       CharacterName,
       Reason,
       ExpiresAtUtc,
       CreatedAtUtc
FROM admin.vw_ActiveBans
WHERE AccountId = @AccountId;
END;
