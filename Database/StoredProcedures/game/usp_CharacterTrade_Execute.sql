-- All statements commit as one transaction -- never a "items moved but money didn't" half-state.
-- TradeSession has already computed both sides' final contents.
--
-- Also writes the trade audit trail (GL_615_TRADE_ITEM/GL_615_TRADE_ITEM2/GL_616_TRADE_MONEY parity,
-- EventLogCategory.Trade) in the same transaction: @TradedItemsA/@TradedItemsB are each side's finalized
-- trade-window offer (up to 8 slots, MAX_TRADE_SLOT_NUM), logged one row per occupied slot (ItemId > 0);
-- @OfferedMoneyA/@OfferedBigMoneyA/@OfferedMoneyB/@OfferedBigMoneyB are each side's own raw contributed money
-- offer, logged only when a side contributed a nonzero amount of either component -- matching legacy's own
-- >0 gate (Server/ts25zone/S04_MyWork02.cpp:8992,9003,8987,8998). Outcome = 1 (success) is the only value
-- ever logged: both money-adjustment guards above already THROW (50268/50269) before this point on any
-- failure, so there is nothing to log on a rejected attempt.
CREATE PROCEDURE game.usp_CharacterTrade_Execute @CharacterA INT,
                                                 @ItemsA0 game.tvp_CharacterItemSlot READONLY,
                                                 @ItemsA1 game.tvp_CharacterItemSlot READONLY,
                                                 @DeltaMoneyA BIGINT,
                                                 @DeltaBigMoneyA INT,
                                                 @CharacterB INT,
                                                 @ItemsB0 game.tvp_CharacterItemSlot READONLY,
                                                 @ItemsB1 game.tvp_CharacterItemSlot READONLY,
                                                 @DeltaMoneyB BIGINT,
                                                 @DeltaBigMoneyB INT,
                                                 @TradedItemsA game.tvp_CharacterItemSlot READONLY,
                                                 @TradedItemsB game.tvp_CharacterItemSlot READONLY,
                                                 @OfferedMoneyA BIGINT = 0,
                                                 @OfferedBigMoneyA INT = 0,
                                                 @OfferedMoneyB BIGINT = 0,
                                                 @OfferedBigMoneyB INT = 0
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

    DELETE FROM game.CharacterItems WHERE CharacterId = @CharacterA AND Container = 1;
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

    DELETE FROM game.CharacterItems WHERE CharacterId = @CharacterB AND Container = 0;
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

    DELETE FROM game.CharacterItems WHERE CharacterId = @CharacterB AND Container = 1;
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

    -- Audit logging (GL_615_TRADE_ITEM/GL_615_TRADE_ITEM2/GL_616_TRADE_MONEY parity) -- both characters are
    -- confirmed to exist by the two guarded UPDATEs above, so these SELECTs always resolve an account id.
    DECLARE @AccountA INT, @AccountB INT;
    SELECT @AccountA = AccountId FROM game.Characters WHERE CharacterId = @CharacterA;
    SELECT @AccountB = AccountId FROM game.Characters WHERE CharacterId = @CharacterB;

    -- Character A's offered items -> B. Empty/omitted @TradedItemsA (no rows with ItemId > 0) produces
    -- nothing, matching legacy's own empty-slot skip.
    INSERT INTO game.EventLog (EventCode, Category, ActorAccountId, ActorCharacterId, TargetAccountId,
                               TargetCharacterId, ItemId, Quantity, Outcome, Payload)
    SELECT 1, -- item transfer (GL_615_TRADE_ITEM/GL_615_TRADE_ITEM2)
           0, -- EventLogCategory.Trade
           @AccountA,
           @CharacterA,
           @AccountB,
           @CharacterB,
           ItemId,
           Quantity,
           1,
           CONCAT(N'Enchant=', Enchant, N';Combine=', Combine, N';Refine=', Refine, N';Socket=', Socket,
                  N';SocketGem1=', SocketGem1, N';SocketGem2=', SocketGem2, N';SocketGem3=', SocketGem3,
                  N';Serial=', Serial)
    FROM @TradedItemsA
    WHERE ItemId > 0;

    -- Character B's offered items -> A (symmetric).
    INSERT INTO game.EventLog (EventCode, Category, ActorAccountId, ActorCharacterId, TargetAccountId,
                               TargetCharacterId, ItemId, Quantity, Outcome, Payload)
    SELECT 1,
           0,
           @AccountB,
           @CharacterB,
           @AccountA,
           @CharacterA,
           ItemId,
           Quantity,
           1,
           CONCAT(N'Enchant=', Enchant, N';Combine=', Combine, N';Refine=', Refine, N';Socket=', Socket,
                  N';SocketGem1=', SocketGem1, N';SocketGem2=', SocketGem2, N';SocketGem3=', SocketGem3,
                  N';Serial=', Serial)
    FROM @TradedItemsB
    WHERE ItemId > 0;

    -- Character A's offered money -> B. No row at all when A contributed zero of both components.
    IF @OfferedMoneyA > 0 OR @OfferedBigMoneyA > 0
        EXEC game.usp_EventLog_Insert
             @EventCode = 2, -- money transfer (GL_616_TRADE_MONEY)
             @Category = 0, -- EventLogCategory.Trade
             @ActorAccountId = @AccountA,
             @ActorCharacterId = @CharacterA,
             @TargetAccountId = @AccountB,
             @TargetCharacterId = @CharacterB,
             @DeltaMoney = @OfferedMoneyA,
             @DeltaBigMoney = @OfferedBigMoneyA,
             @Outcome = 1;

    -- Character B's offered money -> A (symmetric).
    IF @OfferedMoneyB > 0 OR @OfferedBigMoneyB > 0
        EXEC game.usp_EventLog_Insert
             @EventCode = 2,
             @Category = 0,
             @ActorAccountId = @AccountB,
             @ActorCharacterId = @CharacterB,
             @TargetAccountId = @AccountA,
             @TargetCharacterId = @CharacterA,
             @DeltaMoney = @OfferedMoneyB,
             @DeltaBigMoney = @OfferedBigMoneyB,
             @Outcome = 1;

    COMMIT TRANSACTION;
END;
