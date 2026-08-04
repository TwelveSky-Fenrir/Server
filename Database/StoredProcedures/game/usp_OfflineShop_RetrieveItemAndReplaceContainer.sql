CREATE PROCEDURE game.usp_OfflineShop_RetrieveItemAndReplaceContainer @CharacterId INT,
                                                                      @SlotIndex SMALLINT,
                                                                      @ExpectedItemId INT,
                                                                      @ExpectedQuantity INT,
                                                                      @ExpectedValue INT,
                                                                      @ExpectedSerialNumber INT,
                                                                      @ExpectedSocketGem1 INT,
                                                                      @ExpectedSocketGem2 INT,
                                                                      @ExpectedSocketGem3 INT,
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
      AND SerialNumber = @ExpectedSerialNumber
      AND SocketGem1 = @ExpectedSocketGem1
      AND SocketGem2 = @ExpectedSocketGem2
      AND SocketGem3 = @ExpectedSocketGem3
      AND EXISTS (SELECT 1 FROM game.OfflineShops WHERE CharacterId = @CharacterId AND ShopState = 0);

    IF
        @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT Applied = CAST(0 AS INT);
            RETURN;
        END;

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

    SELECT Applied = CAST(1 AS INT);
END;
