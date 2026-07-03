-- Contract: @CharacterId INT -> RS0, 0..n rows. Same shape as admin.usp_Ban_GetActiveForAccount but for
-- a character-specific ban (a GM can ban one alt without banning the whole account) -- called at
-- world-entry time (alongside game.usp_Character_GetForWorldEntry), not at login, since the character
-- isn't selected yet at login time.
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
