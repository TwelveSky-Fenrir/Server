-- All 6 statements (2 money adjustments + 4 container replaces) commit as one transaction -- never a
-- "items moved but money didn't" half-state. TradeSession has already computed both sides' final contents.
CREATE PROCEDURE game.usp_CharacterTrade_Execute @CharacterA     INT,
    @ItemsA0        game.tvp_CharacterItemSlot READONLY,
    @ItemsA1        game.tvp_CharacterItemSlot READONLY,
    @DeltaMoneyA    BIGINT,
    @DeltaBigMoneyA INT,
    @CharacterB     INT,
    @ItemsB0        game.tvp_CharacterItemSlot READONLY,
    @ItemsB1        game.tvp_CharacterItemSlot READONLY,
    @DeltaMoneyB    BIGINT,
    @DeltaBigMoneyB INT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

BEGIN
TRANSACTION;

UPDATE game.Characters
SET Money        = Money + @DeltaMoneyA,
    BigMoney     = BigMoney + @DeltaBigMoneyA,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE CharacterId = @CharacterA
  AND Money + @DeltaMoneyA >= 0
  AND BigMoney + @DeltaBigMoneyA >= 0;

IF
@@ROWCOUNT = 0
        THROW 50268, N'Character A: unknown character or insufficient money balance for this trade.', 1;

UPDATE game.Characters
SET Money        = Money + @DeltaMoneyB,
    BigMoney     = BigMoney + @DeltaBigMoneyB,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE CharacterId = @CharacterB
  AND Money + @DeltaMoneyB >= 0
  AND BigMoney + @DeltaBigMoneyB >= 0;

IF
@@ROWCOUNT = 0
        THROW 50269, N'Character B: unknown character or insufficient money balance for this trade.', 1;

DELETE
FROM game.CharacterItems
WHERE CharacterId = @CharacterA
  AND Container = 0;
INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                 Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
SELECT @CharacterA,
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
       Serial
FROM @ItemsA0;

DELETE
FROM game.CharacterItems
WHERE CharacterId = @CharacterA
  AND Container = 1;
INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                 Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
SELECT @CharacterA,
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
       Serial
FROM @ItemsA1;

DELETE
FROM game.CharacterItems
WHERE CharacterId = @CharacterB
  AND Container = 0;
INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                 Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
SELECT @CharacterB,
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
       Serial
FROM @ItemsB0;

DELETE
FROM game.CharacterItems
WHERE CharacterId = @CharacterB
  AND Container = 1;
INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                 Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
SELECT @CharacterB,
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
       Serial
FROM @ItemsB1;

COMMIT TRANSACTION;
END;
