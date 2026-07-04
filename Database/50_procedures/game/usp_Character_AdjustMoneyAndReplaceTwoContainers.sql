-- database/50_procedures/game/usp_Character_AdjustMoneyAndReplaceTwoContainers.sql
-- Contract: atomically (1) adjust a character's Money/BigMoney pools and (2) whole-container replace TWO
-- of their item containers, in a SINGLE transaction. D7 regime (b) -- used when the target and material
-- slots of an IMPROVE_ITEM (enchant) attempt land on DIFFERENT inventory pages (Page1 != Page2): the cost
-- (silver), the target item's mutated Enchant byte, and the consumed/emptied material slot must all commit
-- or roll back together, exactly like usp_Character_AdjustMoneyAndReplaceContainer does for the
-- single-container case (same-page enchant, or NPC shop buy/sell).
-- Params:
--   @CharacterId   INT
--   @DeltaMoney    BIGINT  -- negative = cost paid; enchant never credits money, but the shape stays general
--   @DeltaBigMoney INT
--   @ContainerA    TINYINT -- must differ from @ContainerB
--   @ItemsA        game.tvp_CharacterItemSlot READONLY
--   @ContainerB    TINYINT
--   @ItemsB        game.tvp_CharacterItemSlot READONLY
-- Result set: none.
-- Idempotent: no (same posture as usp_Character_AdjustMoney).
-- DELETE-then-INSERT per container (never MERGE); XACT_ABORT ON guarantees any error rolls the WHOLE
-- five-statement group back.
-- Errors:
--   THROW 50261 -- the adjustment would drive Money above MAX_NUMBER_SIZE (shared guard, see
--                  usp_Character_AdjustMoney).
--   THROW 50265 -- unknown character or insufficient money balance for this adjustment.
--   THROW 50266 -- @ContainerA equals @ContainerB (caller error: use
--                  usp_Character_AdjustMoneyAndReplaceContainer for a same-container case instead).
CREATE PROCEDURE game.usp_Character_AdjustMoneyAndReplaceTwoContainers @CharacterId   INT,
    @DeltaMoney    BIGINT,
    @DeltaBigMoney INT,
    @ContainerA    TINYINT,
    @ItemsA        game.tvp_CharacterItemSlot READONLY,
    @ContainerB    TINYINT,
    @ItemsB        game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

    IF
@ContainerA = @ContainerB
        THROW 50266, N'ContainerA and ContainerB must differ -- use usp_Character_AdjustMoneyAndReplaceContainer for a same-container case.', 1;

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
50265, N'Unknown character or insufficient money balance for this adjustment.', 1;
END;

DELETE
FROM game.CharacterItems
WHERE CharacterId = @CharacterId
  AND Container = @ContainerA;

INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity,
                                 Enchant, Combine, Refine, Socket,
                                 SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
SELECT @CharacterId,
       @ContainerA,
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
FROM @ItemsA;

DELETE
FROM game.CharacterItems
WHERE CharacterId = @CharacterId
  AND Container = @ContainerB;

INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity,
                                 Enchant, Combine, Refine, Socket,
                                 SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
SELECT @CharacterId,
       @ContainerB,
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
FROM @ItemsB;

COMMIT TRANSACTION;
END;
