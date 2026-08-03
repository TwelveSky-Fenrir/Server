CREATE OR ALTER PROCEDURE game.usp_AccountVault_TransferItemWithCharacter @CharacterId INT,
                                                                          @Container TINYINT,
                                                                          @Items game.tvp_CharacterItemSlot READONLY,
                                                                          @AccountId INT,
                                                                          @VaultItems game.tvp_AccountVaultItemSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DELETE
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @Container;

    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity,
                                     Enchant, Combine, Refine, Socket,
                                     SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
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
           Serial
    FROM @Items;

    IF NOT EXISTS (SELECT 1 FROM game.AccountVault WHERE AccountId = @AccountId)
        BEGIN
            BEGIN TRY
                INSERT INTO game.AccountVault (AccountId) VALUES (@AccountId);
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() NOT IN (2627, 2601)
                    THROW;

                ROLLBACK TRANSACTION;

                BEGIN TRANSACTION;

                DELETE
                FROM game.CharacterItems
                WHERE CharacterId = @CharacterId
                  AND Container = @Container;

                INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity,
                                                 Enchant, Combine, Refine, Socket,
                                                 SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
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
                       Serial
                FROM @Items;

                DELETE
                FROM game.AccountVaultItems
                WHERE AccountId = @AccountId;

                INSERT INTO game.AccountVaultItems (AccountId, SlotIndex, ItemId, Quantity, Value, SerialNumber,
                                                    SocketData)
                SELECT @AccountId,
                       SlotIndex,
                       ItemId,
                       Quantity,
                       Value,
                       SerialNumber,
                       SocketData
                FROM @VaultItems;

                COMMIT TRANSACTION;

                RETURN;
            END CATCH;
        END;

    DELETE
    FROM game.AccountVaultItems
    WHERE AccountId = @AccountId;

    INSERT INTO game.AccountVaultItems (AccountId, SlotIndex, ItemId, Quantity, Value, SerialNumber, SocketData)
    SELECT @AccountId,
           SlotIndex,
           ItemId,
           Quantity,
           Value,
           SerialNumber,
           SocketData
    FROM @VaultItems;

    COMMIT TRANSACTION;
END;
