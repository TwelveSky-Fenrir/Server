CREATE PROCEDURE game.usp_Character_PersistRunes @CharacterId INT,
                                                 @Container TINYINT,
                                                 @Runes game.tvp_CharacterRuneSocket READONLY,
                                                 @Items game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    BEGIN
        TRANSACTION;

    DELETE
    FROM game.CharacterRunes
    WHERE CharacterId = @CharacterId;

    INSERT INTO game.CharacterRunes (CharacterId, SocketIndex, RuneItemId, RuneStat)
    SELECT @CharacterId,
           SocketIndex,
           RuneItemId,
           RuneStat
    FROM @Runes;

    IF @Container <> 255
        BEGIN
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
        END;

    COMMIT TRANSACTION;
END;
