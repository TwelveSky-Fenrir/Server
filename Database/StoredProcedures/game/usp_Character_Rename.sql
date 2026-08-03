CREATE PROCEDURE game.usp_Character_Rename @AccountId INT,
                                           @Slot TINYINT,
                                           @NewName NVARCHAR(13),
                                           @ItemContainer TINYINT,
                                           @ItemSlot TINYINT,
                                           @RequiredItemId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @CharacterId INT;

    SELECT @CharacterId = CharacterId
    FROM game.Characters
    WHERE AccountId = @AccountId
      AND Slot = @Slot;

    IF @CharacterId IS NULL
        BEGIN
            SELECT 102;
            RETURN;
        END;

    IF NOT EXISTS (SELECT 1
                   FROM game.CharacterItems
                   WHERE CharacterId = @CharacterId
                     AND Container = @ItemContainer
                     AND Slot = @ItemSlot
                     AND ItemId = @RequiredItemId)
        BEGIN
            SELECT -1;
            RETURN;
        END;

    IF EXISTS (SELECT 1 FROM game.Characters WHERE Name = @NewName)
        BEGIN
            SELECT 2;
            RETURN;
        END;

    BEGIN TRANSACTION;

    UPDATE game.Characters
    SET Name         = @NewName,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId;

    IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT 102;
            RETURN;
        END;

    DELETE
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @ItemContainer
      AND Slot = @ItemSlot;

    COMMIT TRANSACTION;

    SELECT 0;
END;
