-- Returns legacy wire codes (mDB.ChangeCharacterName) directly instead of THROW: 0 renamed, 2 name
-- taken, 102 no character at that slot. The handler forwards these verbatim to the client.
CREATE PROCEDURE game.usp_Character_Rename @AccountId INT,
                                           @Slot TINYINT,
                                           @NewName NVARCHAR(13)
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF
        EXISTS (SELECT 1 FROM game.Characters WHERE Name = @NewName)
        BEGIN
            SELECT 2;
            RETURN;
        END;

    UPDATE game.Characters
    SET Name = @NewName
    WHERE AccountId = @AccountId
      AND Slot = @Slot;

    IF
        @@ROWCOUNT = 0
        BEGIN
            SELECT 102;
            RETURN;
        END;

    SELECT 0;
END;
