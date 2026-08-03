CREATE PROCEDURE game.usp_Character_AdjustMoneyAndReplaceTwoContainers @CharacterId INT,
                                                                       @DeltaMoney BIGINT,
                                                                       @DeltaBigMoney INT,
                                                                       @ContainerA TINYINT,
                                                                       @ItemsA game.tvp_CharacterItemSlot READONLY,
                                                                       @ContainerB TINYINT,
                                                                       @ItemsB game.tvp_CharacterItemSlot READONLY
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
                                     SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial, XPos, YPos)
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
           Serial,
           XPos,
           YPos
    FROM @ItemsA;

    DELETE
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @ContainerB;

    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity,
                                     Enchant, Combine, Refine, Socket,
                                     SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial, XPos, YPos)
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
           Serial,
           XPos,
           YPos
    FROM @ItemsB;

    COMMIT TRANSACTION;
END;
