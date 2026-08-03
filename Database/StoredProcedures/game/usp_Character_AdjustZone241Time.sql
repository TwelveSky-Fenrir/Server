CREATE PROCEDURE game.usp_Character_AdjustZone241Time @CharacterId INT,
                                                      @Delta INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Adjusted TABLE
                      (
                          Zone241Time INT
                      );

    UPDATE game.Characters
    SET Zone241Time  = Zone241Time + @Delta,
        UpdatedAtUtc = SYSUTCDATETIME()
    OUTPUT INSERTED.Zone241Time INTO @Adjusted
    WHERE CharacterId = @CharacterId
      AND Zone241Time + @Delta >= 0;

    IF @@ROWCOUNT = 0
        THROW 50336, N'Unknown character or insufficient Zone241Time balance for this adjustment.', 1;

    SELECT Zone241Time AS NewZone241Time FROM @Adjusted;
END;
