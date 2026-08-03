CREATE PROCEDURE game.usp_CharacterPetBag_Deposit @CharacterId INT,
                                                  @InventoryContainer TINYINT,
                                                  @InventoryItems game.tvp_CharacterItemSlot READONLY,
                                                  @PetBagSlot TINYINT,
                                                  @PetItemId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    IF EXISTS (SELECT 1 FROM game.CharacterPetBag WHERE CharacterId = @CharacterId AND Slot = @PetBagSlot)
        THROW 50350, N'Pet-bag destination slot already occupied.', 1;

    DELETE
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @InventoryContainer;

    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine, Refine,
                                     Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial, XPos, YPos)
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
           Serial,
           XPos,
           YPos
    FROM @InventoryItems;

    INSERT INTO game.CharacterPetBag (CharacterId, Slot, ItemId)
    VALUES (@CharacterId, @PetBagSlot, @PetItemId);

    COMMIT TRANSACTION;
END;
