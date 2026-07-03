-- database/50_procedures/game/usp_CharacterItems_ReplaceContainer.sql
-- Contract: whole-container replace of one character's item slots (the legacy saves each avatar item
-- array as a block, never slot-by-slot -- same semantics as game.usp_AccountVault_SetItems, scoped to
-- one Container enum value; see game.CharacterItems for the enum).
-- Params:
--   @CharacterId INT
--   @Container   TINYINT -- 0=InventoryPage0, 1=InventoryPage1, 2=Equipment, 3=Store
--   @Items       game.tvp_CharacterItemSlot READONLY -- one row per OCCUPIED slot; an omitted/empty TVP
--     is the legal way to clear the whole container (READONLY TVPs default to empty when not supplied).
-- Result set: none.
-- Idempotent: yes -- replaying the same @Items list is a no-op in effect.
-- DELETE-then-INSERT, never MERGE (architecture reference §12.3) -- but unlike usp_AccountVault_SetItems
-- this pair is wrapped in an EXPLICIT transaction: a fault between the two statements must never be able
-- to commit the DELETE alone (a wiped equipment container is destroyed player value, D7 regime (b)).
-- XACT_ABORT ON guarantees any error rolls the whole pair back.
-- Errors: none of its own (FK/CK violations surface raw -- a bad ItemId/slot is caller corruption, not
-- business flow).
CREATE PROCEDURE game.usp_CharacterItems_ReplaceContainer @CharacterId INT,
    @Container   TINYINT,
    @Items       game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

BEGIN
TRANSACTION;

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
