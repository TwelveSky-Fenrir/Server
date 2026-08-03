CREATE PROCEDURE game.usp_Character_SpendBloodCoinAndReplaceContainer @CharacterId INT,
                                                                      @DeltaBloodCoin INT,
                                                                      @Container TINYINT,
                                                                      @Items game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    BEGIN
        TRANSACTION;

    DECLARE
        @Debited TABLE
                 (
                     BloodCoin INT
                 );

    UPDATE game.Characters
    SET BloodCoin    = BloodCoin + @DeltaBloodCoin,
        UpdatedAtUtc = SYSUTCDATETIME()
    OUTPUT INSERTED.BloodCoin
        INTO @Debited
    WHERE CharacterId = @CharacterId
      AND BloodCoin + @DeltaBloodCoin >= 0;

    IF
        @@ROWCOUNT = 0
        THROW 50271, N'Unknown character or insufficient BloodCoin balance for this adjustment.', 1;

    DELETE
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @Container;

    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                     Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial, XPos, YPos)
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

    SELECT BloodCoin AS NewBloodCoin
    FROM @Debited;

    COMMIT TRANSACTION;
END;
