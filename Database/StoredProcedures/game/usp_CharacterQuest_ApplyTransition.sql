-- @DeltaMoney: unlike every other money proc, a cap breach here is silently skipped (Money left
-- unchanged), never thrown -- this matches verified legacy quest-reward semantics, not an oversight.
-- @Container1/@Container2 = 255 means "do not touch"; passing the same id in both is a caller error.
CREATE PROCEDURE game.usp_CharacterQuest_ApplyTransition @CharacterId INT,
                                                         @StepPermanent INT,
                                                         @ActiveQuestId INT,
                                                         @QSort INT,
                                                         @TargetPhase INT,
                                                         @KillCounter INT,
                                                         @DeltaMoney BIGINT = 0,
                                                         @Container1 TINYINT = 255,
                                                         @Items1 game.tvp_CharacterItemSlot READONLY,
                                                         @Container2 TINYINT = 255,
                                                         @Items2 game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    BEGIN
        TRANSACTION;

    UPDATE game.CharacterQuests
    SET StepPermanent = @StepPermanent,
        ActiveQuestId = @ActiveQuestId,
        QSort         = @QSort,
        TargetPhase   = @TargetPhase,
        KillCounter   = @KillCounter
    WHERE CharacterId = @CharacterId;

    IF
        @@ROWCOUNT = 0
        INSERT INTO game.CharacterQuests (CharacterId, StepPermanent, ActiveQuestId, QSort, TargetPhase, KillCounter)
        VALUES (@CharacterId, @StepPermanent, @ActiveQuestId, @QSort, @TargetPhase, @KillCounter);

    IF
        @DeltaMoney <> 0
        UPDATE game.Characters
        SET Money        = Money + @DeltaMoney,
            UpdatedAtUtc = SYSUTCDATETIME()
        WHERE CharacterId = @CharacterId
          AND Money + @DeltaMoney BETWEEN 0 AND 2000000000;
-- Deliberately no error branch: a cap breach or vanished character leaves Money untouched (see header).

    IF
        @Container1 <> 255
        BEGIN
            DELETE
            FROM game.CharacterItems
            WHERE CharacterId = @CharacterId
              AND Container = @Container1;

            INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity,
                                             Enchant, Combine, Refine, Socket,
                                             SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
            SELECT @CharacterId,
                   @Container1,
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
            FROM @Items1;
        END;

    IF
        @Container2 <> 255
        BEGIN
            DELETE
            FROM game.CharacterItems
            WHERE CharacterId = @CharacterId
              AND Container = @Container2;

            INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity,
                                             Enchant, Combine, Refine, Socket,
                                             SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
            SELECT @CharacterId,
                   @Container2,
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
            FROM @Items2;
        END;

    COMMIT TRANSACTION;
END;
