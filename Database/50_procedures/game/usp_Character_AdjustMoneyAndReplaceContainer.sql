-- database/50_procedures/game/usp_Character_AdjustMoneyAndReplaceContainer.sql
-- Contract: atomically (1) adjust a character's Money/BigMoney pools and (2) whole-container replace ONE
-- of their item containers, in a SINGLE transaction. D7 regime (b) -- the single-character, one-container
-- twin of usp_CharacterTrade_Execute's own two-character/four-container shape. Exists because an NPC-shop
-- buy/sell (V5 NPC & Economy) is exactly the "money AND an item container in one logical client action"
-- case usp_CharacterItems_ReplaceTwoContainers's own header comment calls out: NPC buy pays money and
-- receives an item (one inventory container); NPC sell hands over an item (one inventory container) and
-- receives money. Calling usp_Character_AdjustMoney and usp_CharacterItems_ReplaceContainer as two
-- independent round trips would let a fault between them either take the player's money without ever
-- granting the item, or remove the item without ever crediting the money -- this proc closes that window.
-- Params:
--   @CharacterId   INT
--   @DeltaMoney    BIGINT  -- negative = paid out (buy), positive = received (sell)
--   @DeltaBigMoney INT
--   @Container     TINYINT -- 0=InventoryPage0, 1=InventoryPage1, 2=Equipment, 3=StorePage0, 4=StorePage1
--   @Items         game.tvp_CharacterItemSlot READONLY -- the container's FULL new content
-- Result set: none.
-- Idempotent: no (same posture as usp_Character_AdjustMoney -- a caller must not retry after a successful commit).
-- DELETE-then-INSERT for the container (never MERGE, architecture reference §12.3); XACT_ABORT ON
-- guarantees any error (money guard failing, an FK/CK violation on the INSERT) rolls the WHOLE group back.
-- Errors:
--   THROW 50261 -- the adjustment would drive Money above MAX_NUMBER_SIZE (2,000,000,000) -- shared with
--                  usp_Character_AdjustMoney's own identical guard (NPC-shop SELL is this proc's own credit path).
--   THROW 50264 -- unknown character or insufficient money balance for this adjustment.
CREATE PROCEDURE game.usp_Character_AdjustMoneyAndReplaceContainer @CharacterId   INT,
    @DeltaMoney    BIGINT,
    @DeltaBigMoney INT,
    @Container     TINYINT,
    @Items         game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

BEGIN
TRANSACTION;

    -- Both the lower (>=0) and upper (MAX_NUMBER_SIZE) bounds are folded into this SAME guarded UPDATE, not a
    -- separate pre-check before the transaction started -- a standalone `IF EXISTS` pre-check (a prior pass
    -- here had one) is a real TOCTOU window: two concurrent credits could each read the same pre-update Money
    -- value, both pass their own cap check, and both commit, jointly pushing Money above the cap.
UPDATE game.Characters
SET Money        = Money + @DeltaMoney,
    BigMoney     = BigMoney + @DeltaBigMoney,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE CharacterId = @CharacterId
  AND Money + @DeltaMoney BETWEEN 0 AND 2000000000
  AND BigMoney + @DeltaBigMoney >= 0;

IF
@@ROWCOUNT = 0
BEGIN
        -- Purely diagnostic re-read (no TOCTOU risk: nothing is written based on it) to pick which error code
        -- callers rely on to distinguish a cap breach from plain insufficient funds.
        IF
EXISTS (SELECT 1
                   FROM game.Characters
                   WHERE CharacterId = @CharacterId
                     AND Money + @DeltaMoney > 2000000000)
            THROW 50261, N'Adjustment would exceed the legacy money cap (MAX_NUMBER_SIZE = 2,000,000,000).', 1;

        THROW
50264, N'Unknown character or insufficient money balance for this adjustment.', 1;
END;

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
