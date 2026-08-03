CREATE PROCEDURE game.usp_Character_BuyWarPointItem @CharacterId INT,
                                                    @WarPointCost INT,
                                                    @Container TINYINT,
                                                    @Items game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DECLARE @Debited TABLE
                     (
                         WarPoint INT
                     );

    UPDATE game.Characters
    SET WarPoint     = WarPoint - @WarPointCost,
        UpdatedAtUtc = SYSUTCDATETIME()
    OUTPUT INSERTED.WarPoint
        INTO @Debited
    WHERE CharacterId = @CharacterId
      AND @WarPointCost >= 0
      AND WarPoint >= @WarPointCost;

    IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT CAST(-1 AS INT) AS NewWarPoint;
            RETURN;
        END;

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

    SELECT WarPoint AS NewWarPoint
    FROM @Debited;

    COMMIT TRANSACTION;
END;
