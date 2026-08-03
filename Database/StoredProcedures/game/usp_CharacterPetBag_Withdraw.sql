CREATE PROCEDURE game.usp_CharacterPetBag_Withdraw @CharacterId INT,
                                                   @PetBagSlot TINYINT,
                                                   @InventoryContainer TINYINT,
                                                   @InventoryItems game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM game.CharacterPetBag WHERE CharacterId = @CharacterId AND Slot = @PetBagSlot)
        THROW 50351, N'Pet-bag source slot is empty.', 1;

    DELETE
    FROM game.CharacterPetBag
    WHERE CharacterId = @CharacterId
      AND Slot = @PetBagSlot;

    DELETE
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @InventoryContainer;

    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine, Refine,
                                     Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @CharacterId,
           @InventoryContainer,
           Slot,
           ItemId,
           Quantity,
           Enchant,
           Combine,
           Refine,
           Socket,
           SocketGem1,
           SocketGem2,
           SocketGem3,
           ExpireDate,
           Serial
    FROM @InventoryItems;

    COMMIT TRANSACTION;
END;
