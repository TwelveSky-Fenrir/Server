-- Stamps LiftedAtUtc instead of deleting, preserving the audit row. Idempotent: lifting an
-- already-lifted or unknown MuteId is a silent no-op (the stamp is never overwritten).
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
