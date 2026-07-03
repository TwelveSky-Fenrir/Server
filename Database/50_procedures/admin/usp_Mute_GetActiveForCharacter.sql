-- Contract: @CharacterId INT -> RS0, 0..n rows { MuteId INT, AccountId INT NULL, CharacterId INT NULL,
-- Reason TINYINT, ExpiresAtUtc DATETIME2(3) NULL, CreatedAtUtc DATETIME2(3) }.
-- Called ONCE at world entry (alongside game.usp_Character_GetForWorldEntry) to set the hidden mute
-- flag in PlayerRuntimeState -- report 06 §1.7: Fenrir does NOT re-query per chat message like the
-- legacy ZPP 45 SELECT did. Covers BOTH targeting modes in one call: mutes pinned to this character AND
-- mutes pinned to its owning account (at world entry both are known, and a character must not dodge an
-- account-level mute by being an alt).
-- Active = not lifted AND (no expiry OR expiry in the future).
-- Idempotent: yes (read-only).
-- Errors: none.
CREATE PROCEDURE admin.usp_Mute_GetActiveForCharacter @CharacterId INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT m.MuteId, m.AccountId, m.CharacterId, m.Reason, m.ExpiresAtUtc, m.CreatedAtUtc
FROM admin.Mutes AS m
WHERE m.LiftedAtUtc IS NULL
  AND (m.ExpiresAtUtc IS NULL OR m.ExpiresAtUtc > SYSUTCDATETIME())
  AND (m.CharacterId = @CharacterId
    OR m.AccountId = (SELECT AccountId
                      FROM game.Characters
                      WHERE CharacterId = @CharacterId));
END;
