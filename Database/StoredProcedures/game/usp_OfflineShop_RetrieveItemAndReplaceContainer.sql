CREATE PROCEDURE game.usp_OfflineShop_RetrieveItemAndReplaceContainer @CharacterId INT,
                                                                      @SlotIndex SMALLINT,
                                                                      @ExpectedItemId INT,
                                                                      @ExpectedQuantity INT,
                                                                      @ExpectedValue INT,
                                                                      @Container TINYINT,
                                                                      @Items game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    BEGIN
        TRANSACTION;

    DELETE
    FROM game.OfflineShopItems
    WHERE CharacterId = @CharacterId
      AND SlotIndex = @SlotIndex
      AND ItemId = @ExpectedItemId
      AND Quantity = @ExpectedQuantity
      AND Value = @ExpectedValue
      AND EXISTS (SELECT 1 FROM game.OfflineShops WHERE CharacterId = @CharacterId AND ShopState = 0);

    IF
        @@ROWCOUNT = 0
        THROW 50272, N'Offline shop is not closed, or the slot no longer matches the expected item.', 1;

    DELETE
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @Container;
    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                     Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial, XPos, YPos)
    SELECT @CharacterId,
           @Container,
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
    FROM @Items;

    COMMIT TRANSACTION;
END;
