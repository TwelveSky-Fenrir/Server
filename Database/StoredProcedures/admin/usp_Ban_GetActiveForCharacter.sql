-- Character-specific ban (a GM can ban one alt without banning the account); checked at world-entry
-- time since the character isn't selected yet at login.
CREATE PROCEDURE admin.usp_Ban_GetActiveForCharacter @CharacterId INT
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
WHERE CharacterId = @CharacterId;
END;
