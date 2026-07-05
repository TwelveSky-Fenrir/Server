-- database/50_procedures/game/usp_PshopPurchase_Execute.sql
-- Live PShop trades never touch BigMoney (verified S04_MyWork02.cpp:7056/7073). No item-slot CAS guard
-- here: the caller already re-validated the seller's live inventory under both participants'
-- EconomyActionLock before calling.
CREATE PROCEDURE game.usp_PshopPurchase_Execute @SellerCharacterId INT,
    @SellerContainer   TINYINT,
    @SellerItems       game.tvp_CharacterItemSlot READONLY,
    @BuyerCharacterId  INT,
    @BuyerContainer    TINYINT,
    @BuyerItems        game.tvp_CharacterItemSlot READONLY,
    @Price             INT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

BEGIN
TRANSACTION;

UPDATE game.Characters
SET Money        = Money - @Price,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE CharacterId = @BuyerCharacterId
  AND Money - @Price BETWEEN 0 AND 2000000000;

IF
@@ROWCOUNT = 0
        THROW 50222, N'Unknown buyer character or insufficient money balance for this purchase.', 1;

UPDATE game.Characters
SET Money        = Money + @Price,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE CharacterId = @SellerCharacterId
  AND Money + @Price BETWEEN 0 AND 2000000000;

IF
@@ROWCOUNT = 0
        THROW 50275, N'Unknown seller character, or the sale would exceed the legacy money cap.', 1;

DELETE
FROM game.CharacterItems
WHERE CharacterId = @SellerCharacterId
  AND Container = @SellerContainer;
INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                 Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
SELECT @SellerCharacterId,
       @SellerContainer,
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
FROM @SellerItems;

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

COMMIT TRANSACTION;
END;
