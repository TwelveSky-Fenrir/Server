-- Called on the login path to catch an active ban that predates or overrides auth.Accounts.IsBanned.
-- Bans are a log, so more than one row can theoretically come back; caller treats any row as banned.
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
