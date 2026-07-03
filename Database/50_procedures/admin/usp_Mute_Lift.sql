-- Contract: @MuteId INT -> no result set. Lifts one mute early by stamping LiftedAtUtc, preserving the
-- audit row (a GM un-mute must not erase the fact the mute happened -- see admin.Mutes).
-- Idempotent: yes -- lifting an already-lifted or unknown MuteId is a silent no-op (the first
-- LiftedAtUtc stamp is never overwritten, so replaying a lift cannot rewrite history), same §12.3
-- silent-no-op contract as usp_Character_Delete.
-- Errors: none.
CREATE PROCEDURE admin.usp_Mute_Lift @MuteId INT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

UPDATE admin.Mutes
SET LiftedAtUtc = SYSUTCDATETIME()
WHERE MuteId = @MuteId
  AND LiftedAtUtc IS NULL;
END;
