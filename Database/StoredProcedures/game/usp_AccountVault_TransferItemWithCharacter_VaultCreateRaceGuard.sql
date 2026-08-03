-- Additive script: usp_AccountVault_TransferItemWithCharacter.sql stays unchanged (DbMigrator journals it
-- by SHA-256 and would refuse to reapply it if edited). CREATE OR ALTER on the same procedure name, same
-- pattern as usp_AccountVault_TransferMoneyWithCharacter_VaultCreateRaceGuard.sql (that file's own header
-- carries the full race explanation this one shares -- identical AccountVault bootstrap shape).
--
-- Here the bootstrap INSERT sits between the two container replaces (CharacterItems already
-- DELETE+INSERTed, AccountVaultItems not yet touched), so a losing 2627/2601 that dooms the whole
-- transaction (XACT_STATE() = -1) would otherwise silently discard the already-applied CharacterItems
-- replace too if left uncorrected. The CATCH therefore ROLLBACK TRANSACTIONs and replays BOTH container
-- replaces from @Items/@VaultItems in a fresh transaction, skipping the now-unnecessary AccountVault
-- existence check (the winner's INSERT is guaranteed committed by the time our own INSERT could raise the
-- duplicate-key error) -- so the caller still gets a single atomic replace of both containers, never a
-- partial commit of one side without the other.
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
