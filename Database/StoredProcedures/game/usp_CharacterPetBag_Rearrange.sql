-- One of 3 atomic procedures backing PetBagRepository (Infrastructure/Fenrir.Data/Inventory/PetBagRepository.cs)
-- for the CZ_PROCESS_DATA_SEND pet-bag family (tSort 254/255/256). See usp_CharacterPetBag_Deposit's own
-- header for the shared TOCTOU-closing rationale.
CREATE PROCEDURE game.usp_CharacterPetBag_Rearrange @CharacterId     INT,
                                                     @SourceSlot      TINYINT,
                                                     @DestinationSlot TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DECLARE @ItemId INT;
    SELECT @ItemId = ItemId
    FROM game.CharacterPetBag
    WHERE CharacterId = @CharacterId
      AND Slot = @SourceSlot;

    IF @ItemId IS NULL
        THROW 50351, N'Pet-bag source slot is empty.', 1;

    IF EXISTS (SELECT 1 FROM game.CharacterPetBag WHERE CharacterId = @CharacterId AND Slot = @DestinationSlot)
        THROW 50350, N'Pet-bag destination slot already occupied.', 1;

    DELETE FROM game.CharacterPetBag
    WHERE CharacterId = @CharacterId
      AND Slot = @SourceSlot;

    INSERT INTO game.CharacterPetBag (CharacterId, Slot, ItemId)
    VALUES (@CharacterId, @DestinationSlot, @ItemId);

    COMMIT TRANSACTION;
END;
