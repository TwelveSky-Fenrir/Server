CREATE OR ALTER PROCEDURE game.usp_OfflineShop_ExecutePurchase @SellerCharacterId INT,
                                                               @SlotIndex SMALLINT,
                                                               @ExpectedItemId INT,
                                                               @ExpectedQuantity INT,
                                                               @ExpectedValue INT,
                                                               @Price INT,
                                                               @BuyerCharacterId INT,
                                                               @BuyerContainer TINYINT,
                                                               @BuyerItems game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DELETE
    FROM game.OfflineShopItems
    WHERE CharacterId = @SellerCharacterId
      AND SlotIndex = @SlotIndex
      AND ItemId = @ExpectedItemId
      AND Quantity = @ExpectedQuantity
      AND Value = @ExpectedValue
      AND Price = @Price
      AND EXISTS (SELECT 1 FROM game.OfflineShops WHERE CharacterId = @SellerCharacterId AND ShopState = 1);

    IF @@ROWCOUNT = 0
        THROW 50272, N'Offline shop item no longer matches the expected listing, or the shop is not open.', 1;

    UPDATE game.Characters
    SET Money        = Money - @Price,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @BuyerCharacterId
      AND Money - @Price BETWEEN 0 AND 2000000000;

    IF @@ROWCOUNT = 0
        THROW 50222, N'Unknown character or insufficient money balance for this purchase.', 1;

    DELETE
    FROM game.CharacterItems
    WHERE CharacterId = @BuyerCharacterId
      AND Container = @BuyerContainer;
    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                     Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @BuyerCharacterId,
           @BuyerContainer,
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
    FROM @BuyerItems;

    UPDATE game.OfflineShops
    SET Money    = CAST(CASE
                            WHEN CAST(Money AS BIGINT) + CAST(@Price AS BIGINT) > 2000000000
                                THEN CAST(Money AS BIGINT) + CAST(@Price AS BIGINT) - 2000000000
                            ELSE CAST(Money AS BIGINT) + CAST(@Price AS BIGINT)
        END AS INT),
        BigMoney = CASE
                       WHEN CAST(Money AS BIGINT) + CAST(@Price AS BIGINT) > 2000000000 THEN BigMoney + 2
                       ELSE BigMoney
            END
    WHERE CharacterId = @SellerCharacterId
      AND (CASE
               WHEN CAST(Money AS BIGINT) + CAST(@Price AS BIGINT) > 2000000000 THEN BigMoney + 2
               ELSE BigMoney
        END) <= 999;

    IF @@ROWCOUNT = 0
        THROW 50273, N'Crediting the seller''s offline-shop earnings would exceed the BigMoney cap (999).', 1;

    IF NOT EXISTS (SELECT 1 FROM game.OfflineShopItems WHERE CharacterId = @SellerCharacterId)
        UPDATE game.OfflineShops
        SET ShopState = 0
        WHERE CharacterId = @SellerCharacterId
          AND ShopState = 1;

    COMMIT TRANSACTION;
END;
