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

    DECLARE @AccountA INT, @AccountB INT;
    SELECT @AccountA = AccountId FROM game.Characters WHERE CharacterId = @CharacterA;
    SELECT @AccountB = AccountId FROM game.Characters WHERE CharacterId = @CharacterB;

    INSERT INTO game.EventLog (EventCode, Category, ActorAccountId, ActorCharacterId, TargetAccountId,
                               TargetCharacterId, ItemId, Quantity, Outcome, Payload)
    SELECT 1, 
           0, 
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

    IF @OfferedMoneyA > 0 OR @OfferedBigMoneyA > 0
        EXEC game.usp_EventLog_Insert
             @EventCode = 2, 
             @Category = 0, 
             @ActorAccountId = @AccountA,
             @ActorCharacterId = @CharacterA,
             @TargetAccountId = @AccountB,
             @TargetCharacterId = @CharacterB,
             @DeltaMoney = @OfferedMoneyA,
             @DeltaBigMoney = @OfferedBigMoneyA,
             @Outcome = 1;

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
