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
