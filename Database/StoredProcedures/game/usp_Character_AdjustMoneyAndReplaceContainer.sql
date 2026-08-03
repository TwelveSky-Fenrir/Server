CREATE PROCEDURE game.usp_Character_AdjustMoneyAndReplaceContainer @CharacterId INT,
                                                                   @DeltaMoney BIGINT,
                                                                   @DeltaBigMoney INT,
                                                                   @Container TINYINT,
                                                                   @Items game.tvp_CharacterItemSlot READONLY,
                                                                   @AuditAccountId INT = NULL,
                                                                   @AuditEventCode SMALLINT = NULL,
                                                                   @AuditItemId INT = NULL,
                                                                   @AuditQuantity INT = NULL,
                                                                   @AuditPayload NVARCHAR(MAX) = NULL
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

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
                50264, N'Unknown character or insufficient money balance for this adjustment.', 1;
        END;

    DELETE
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @Container;

    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity,
                                     Enchant, Combine, Refine, Socket,
                                     SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial, XPos, YPos)
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
           Serial,
           XPos,
           YPos
    FROM @Items;

    IF @AuditEventCode IS NOT NULL
        EXEC game.usp_EventLog_Insert
             @EventCode = @AuditEventCode,
             @Category = 16,
             @ActorAccountId = @AuditAccountId,
             @ActorCharacterId = @CharacterId,
             @DeltaMoney = @DeltaMoney,
             @ItemId = @AuditItemId,
             @Quantity = @AuditQuantity,
             @Outcome = 1,
             @Payload = @AuditPayload;

    COMMIT TRANSACTION;
END;
