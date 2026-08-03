CREATE PROCEDURE game.usp_Character_GrantTribeTransferPermit @CharacterId INT,
                                                             @Delta INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    DECLARE
        @Adjusted TABLE
                  (
                      TribeTransferPermitCount INT
                  );

    UPDATE game.Characters
    SET TribeTransferPermitCount = TribeTransferPermitCount + @Delta,
        UpdatedAtUtc             = SYSUTCDATETIME()
    OUTPUT INSERTED.TribeTransferPermitCount
        INTO @Adjusted
    WHERE CharacterId = @CharacterId
      AND TribeTransferPermitCount + @Delta >= 0;

    IF
        @@ROWCOUNT = 0
        THROW 50312, N'Unknown character or insufficient TribeTransferPermitCount balance for this adjustment.', 1;

    SELECT TribeTransferPermitCount AS NewTribeTransferPermitCount
    FROM @Adjusted;
END;
