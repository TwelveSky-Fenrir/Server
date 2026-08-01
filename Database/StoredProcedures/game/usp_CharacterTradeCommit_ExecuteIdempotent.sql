-- C8 durable, anti-dupe two-character trade commit -- the "most cautious economy path" variant of
-- usp_CharacterTrade_Execute. Functionally identical settlement (two money adjustments, four container
-- replaces, and the GL_615_TRADE_ITEM/GL_615_TRADE_ITEM2/GL_616_TRADE_MONEY audit rows, all in ONE
-- transaction) with one added guarantee the legacy in-memory swap never had: it commits AT MOST ONCE per
-- confirmed trade, keyed by @TradeToken (game.TradeCommitLedger).
--
-- Why this exists separately (rather than editing the shipped proc): container replaces are DELETE+INSERT of a
-- final state and so are naturally idempotent, but the money legs are ADDITIVE (Money = Money + @Delta). A
-- retry of a confirmed trade against usp_CharacterTrade_Execute would therefore double-apply money -- a dupe.
-- This proc claims @TradeToken as its first write; the UNIQUE index on TradeCommitLedger.TradeToken is the
-- exactly-once backstop. A benign retry short-circuits on the existence fast-path and does nothing.
--
-- @TradeToken is generated ONCE per commit by the caller (TradeCommitToken.NewForCommit) and reused for every
-- retry of that same commit -- a fresh token per attempt would defeat the dedupe. Money guards THROW the same
-- 50268/50269 as usp_CharacterTrade_Execute (identical failure kinds -- character A / character B unknown or
-- insufficient balance), so no new error-catalog codes are introduced.
CREATE PROCEDURE game.usp_CharacterTradeCommit_ExecuteIdempotent @TradeToken UNIQUEIDENTIFIER,
                                                                 @CharacterA INT,
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

    -- Idempotency fast-path: this exact confirmed trade already settled -> benign no-op. Never re-apply the
    -- additive money legs. Runs outside any transaction (a pure read, no writes to unwind).
    IF EXISTS (SELECT 1 FROM game.TradeCommitLedger WHERE TradeToken = @TradeToken)
        RETURN;

    BEGIN TRANSACTION;

    -- Claim the token as the FIRST write. The UNIQUE index is the ultimate exactly-once backstop: a concurrent
    -- caller carrying the same token loses this INSERT with 2627 and XACT_ABORT rolls the whole trade back with
    -- nothing applied -- fail-safe (no double money/items). TradeLockHandler's per-pair EconomyActionLock makes
    -- that race unreachable in practice; this is defence in depth. A subsequent 50268/50269 THROW below also
    -- rolls this row back, so a failed trade leaves no ledger entry and a legitimate retry can still proceed.
    INSERT INTO game.TradeCommitLedger (TradeToken, CharacterA, CharacterB)
    VALUES (@TradeToken, @CharacterA, @CharacterB);

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
