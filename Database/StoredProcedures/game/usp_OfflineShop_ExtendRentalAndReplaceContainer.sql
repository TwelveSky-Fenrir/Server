-- Bundles the rental-date UPDATE (previously usp_OfflineShop_ExtendRental's whole job) with the paying
-- extension-scroll's item-container replace in one transaction. Closes the transaction-composition-audit
-- finding "proxy-shop rental extension has the same asymmetric-try/catch dupe window": the C# call site
-- (Application/Fenrir.Application.Game.Services/ZoneLifecycle/UseInventoryItemService.cs,
-- ResolveProxyShopRentalExtensionAsync) used to commit usp_OfflineShop_ExtendRental first (inside its own
-- try/catch) then separately call characters.ReplaceContainerAsync (not wrapped) to consume the scroll -- a
-- failure in the second call left the rental durably extended while the paying item stayed present and
-- consumable again on retry. Same shape as usp_Cash_DebitAndGrantItem (game/usp_Cash_DebitAndGrantItem.sql):
-- one economic effect, one container replace, one transaction.
-- Deliberately no @@ROWCOUNT guard on the OfflineShops UPDATE, matching usp_OfflineShop_ExtendRental's own
-- citation (Server/ts25extra/S08_MyDB.cpp:1085-1106): a character with no game.OfflineShops row yet still
-- "succeeds" with nothing persisted for the rental leg, while the item consumption still commits -- this
-- procedure only changes the two writes' atomicity, not that pre-existing legacy-parity behavior.
CREATE PROCEDURE game.usp_OfflineShop_ExtendRentalAndReplaceContainer @CharacterId INT,
                                                                      @ShopDate INT,
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

    UPDATE game.OfflineShops
    SET ShopDate = @ShopDate
    WHERE CharacterId = @CharacterId;

    DELETE
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @Container;

    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity,
                                     Enchant, Combine, Refine, Socket,
                                     SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
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
           Serial
    FROM @Items;

    COMMIT TRANSACTION;
END;
