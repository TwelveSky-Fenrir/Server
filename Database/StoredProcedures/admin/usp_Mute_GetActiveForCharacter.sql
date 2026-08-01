-- Called once at world entry to set the hidden mute flag in PlayerRuntimeState (not re-queried per
-- chat message). Covers mutes pinned to this character AND to its owning account, so an alt can't
-- dodge an account-level mute.
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
