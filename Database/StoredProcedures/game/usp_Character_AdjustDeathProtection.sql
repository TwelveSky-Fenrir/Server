CREATE PROCEDURE game.usp_Character_AdjustDeathProtection @CharacterId INT,
                                                          @Delta INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Adjusted TABLE
                      (
                          ProtectForDeath INT
                      );

    UPDATE game.Characters
    SET ProtectForDeath = ProtectForDeath + @Delta,
        UpdatedAtUtc    = SYSUTCDATETIME()
    OUTPUT INSERTED.ProtectForDeath INTO @Adjusted
    WHERE CharacterId = @CharacterId
      AND ProtectForDeath + @Delta >= 0;

    IF @@ROWCOUNT = 0
        THROW 50332, N'Unknown character or insufficient ProtectForDeath balance for this adjustment.', 1;

    SELECT ProtectForDeath AS NewProtectForDeath FROM @Adjusted;
END;
