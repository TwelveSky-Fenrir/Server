CREATE PROCEDURE game.usp_OfflineShop_OpenAndReplaceContainers @CharacterId INT,
                                                               @ZoneNumber SMALLINT,
                                                               @ShopDate INT,
                                                               @ShopName NVARCHAR(48),
                                                               @LocationX INT,
                                                               @LocationY INT,
                                                               @LocationZ INT,
                                                               @Items game.tvp_OfflineShopItemSlot READONLY,
                                                               @InventoryPage0 game.tvp_CharacterItemSlot READONLY,
                                                               @InventoryPage1 game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    BEGIN
        TRANSACTION;

    IF
        EXISTS (SELECT 1
                FROM game.OfflineShops
                WHERE CharacterId = @CharacterId
                  AND (ShopState = 1 OR Money <> 0 OR BigMoney <> 0
                    OR EXISTS (SELECT 1 FROM game.OfflineShopItems WHERE CharacterId = @CharacterId)))
        THROW 50272, N'An existing offline shop for this character is open or still holds unclaimed items/money.', 1;

    UPDATE game.OfflineShops
    SET ZoneNumber = @ZoneNumber,
        ShopState  = 1,
        ShopDate   = @ShopDate,
        Money      = 0,
        BigMoney   = 0,
        LocationX  = @LocationX,
        LocationY  = @LocationY,
        LocationZ  = @LocationZ,
        ShopName   = @ShopName
    WHERE CharacterId = @CharacterId;

    IF
        @@ROWCOUNT = 0
        INSERT INTO game.OfflineShops (CharacterId, ZoneNumber, ShopState, ShopDate, Money, BigMoney,
                                       LocationX, LocationY, LocationZ, ShopName)
        VALUES (@CharacterId, @ZoneNumber, 1, @ShopDate, 0, 0, @LocationX, @LocationY, @LocationZ, @ShopName);

    INSERT INTO game.OfflineShopItems (CharacterId, SlotIndex, ItemId, Quantity, Value, SerialNumber, Price,
                                       SocketData, SocketGem1, SocketGem2, SocketGem3)
    SELECT @CharacterId,
           SlotIndex,
           ItemId,
           Quantity,
           Value,
           SerialNumber,
           Price,
           SocketData,
           SocketGem1,
           SocketGem2,
           SocketGem3
    FROM @Items;

    DELETE
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = 0;
    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                     Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial, XPos, YPos)
    SELECT @CharacterId,
           0,
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
    FROM @InventoryPage0;

    DELETE
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = 1;
    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                     Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial, XPos, YPos)
    SELECT @CharacterId,
           1,
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
    FROM @InventoryPage1;

    COMMIT TRANSACTION;
END;
