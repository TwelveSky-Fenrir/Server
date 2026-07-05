-- Same TOCTOU-safe money guard as usp_Character_AdjustMoney, plus a whole-container item replace in the
-- same transaction (e.g. NPC shop buy/sell).
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

    -- Guarded UPDATE closes a TOCTOU: two concurrent credits must never jointly breach the cap.
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
        -- Diagnostic re-read only; picks which error code to throw.
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
