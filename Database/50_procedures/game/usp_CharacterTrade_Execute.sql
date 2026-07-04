-- database/50_procedures/game/usp_CharacterTrade_Execute.sql
-- Contract: the atomic two-character trade commit (CZ_TRADE_MENU_SEND, both sides' menu reach state 2 --
-- contracts/05_social.md). D7 regime (b), extended past usp_CharacterItems_ReplaceTwoContainers's
-- two-container/one-character shape: a single trade touches BOTH of each character's inventory pages
-- (an item can only be pulled out of/land back into a full 0-63/64-127 slot inventory, TradeSession never
-- knows in advance which page has a free slot) AND both characters' money pools -- 4 container replaces +
-- 2 money adjustments, all-or-nothing. TradeSession (Fenrir.Application.Game.Social.Trade) has already
-- computed the exact final container contents for both sides (items removed from the offering side,
-- added into the receiving side's free slots) before calling this -- this proc is the durable commit,
-- not the negotiation.
-- Params:
--   @CharacterA      INT
--   @ItemsA0         game.tvp_CharacterItemSlot READONLY -- Character A's FINAL InventoryPage0 (container 0)
--   @ItemsA1         game.tvp_CharacterItemSlot READONLY -- Character A's FINAL InventoryPage1 (container 1)
--   @DeltaMoneyA     BIGINT   -- Character A's money delta (negative = paid out, positive = received)
--   @DeltaBigMoneyA  INT
--   @CharacterB      INT
--   @ItemsB0         game.tvp_CharacterItemSlot READONLY
--   @ItemsB1         game.tvp_CharacterItemSlot READONLY
--   @DeltaMoneyB     BIGINT
--   @DeltaBigMoneyB  INT
-- Result set: none.
-- Idempotent: no (replaying re-applies the money deltas a second time -- TradeSession's commit is a
-- one-shot state transition, same posture as usp_Character_AdjustMoney; a caller must not retry this
-- call after a successful COMMIT).
-- DELETE-then-INSERT per container (never MERGE, architecture reference §12.3); XACT_ABORT ON guarantees
-- any error (a negative-money guard failing, an FK/CK violation on either INSERT) rolls the WHOLE
-- six-statement group back -- never a "A's items moved but B's money didn't" half-state.
-- Errors:
--   THROW 50268 -- the adjustment would drive Character A's Money or BigMoney negative.
--   THROW 50269 -- the adjustment would drive Character B's Money or BigMoney negative.
CREATE PROCEDURE game.usp_CharacterTrade_Execute
    @CharacterA     INT,
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
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    UPDATE game.Characters
    SET Money        = Money + @DeltaMoneyA,
        BigMoney     = BigMoney + @DeltaBigMoneyA,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterA
      AND Money + @DeltaMoneyA >= 0
      AND BigMoney + @DeltaBigMoneyA >= 0;

    IF @@ROWCOUNT = 0
        THROW 50268, N'Character A: unknown character or insufficient money balance for this trade.', 1;

    UPDATE game.Characters
    SET Money        = Money + @DeltaMoneyB,
        BigMoney     = BigMoney + @DeltaBigMoneyB,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterB
      AND Money + @DeltaMoneyB >= 0
      AND BigMoney + @DeltaBigMoneyB >= 0;

    IF @@ROWCOUNT = 0
        THROW 50269, N'Character B: unknown character or insufficient money balance for this trade.', 1;

    DELETE FROM game.CharacterItems WHERE CharacterId = @CharacterA AND Container = 0;
    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                      Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @CharacterA, 0, Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket,
           SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial
    FROM @ItemsA0;

    DELETE FROM game.CharacterItems WHERE CharacterId = @CharacterA AND Container = 1;
    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                      Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @CharacterA, 1, Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket,
           SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial
    FROM @ItemsA1;

    DELETE FROM game.CharacterItems WHERE CharacterId = @CharacterB AND Container = 0;
    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                      Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @CharacterB, 0, Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket,
           SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial
    FROM @ItemsB0;

    DELETE FROM game.CharacterItems WHERE CharacterId = @CharacterB AND Container = 1;
    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                      Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @CharacterB, 1, Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket,
           SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial
    FROM @ItemsB1;

    COMMIT TRANSACTION;
END;
