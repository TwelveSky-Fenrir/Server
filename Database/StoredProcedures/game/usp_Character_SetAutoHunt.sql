-- Stored as an opaque blob, not decomposed into columns: the legacy copies the client struct verbatim with no validation.
CREATE PROCEDURE game.usp_Character_SetAutoHunt @CharacterId INT,
                                                @Enabled BIT,
                                                @Config VARBINARY(112)
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE game.Characters
    SET AutoHuntEnabled = @Enabled,
        AutoHuntConfig  = @Config,
        UpdatedAtUtc    = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId;
END;
