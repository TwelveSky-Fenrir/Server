CREATE PROCEDURE game.usp_CharacterItems_ReplaceTwoContainers @CharacterId INT,
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
        THROW 50260, N'ContainerA and ContainerB must differ -- use usp_CharacterItems_ReplaceContainer for a same-container move.', 1;

    BEGIN
        TRANSACTION;

    DELETE
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @ContainerA;

    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity,
                                     Enchant, Combine, Refine, Socket,
                                     SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
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
           Serial
    FROM @ItemsA;

    DELETE
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @ContainerB;

    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity,
                                     Enchant, Combine, Refine, Socket,
                                     SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
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
           Serial
    FROM @ItemsB;

    COMMIT TRANSACTION;
END;
