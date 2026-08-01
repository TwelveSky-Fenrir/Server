-- Atomic item transfer between one character inventory container (game.CharacterItems) and the owning
-- account's shared vault/bank (game.AccountVault/AccountVaultItems, legacy masterinfo.uSaveItem) --
-- CZ_PROCESS_DATA_SEND tSort 228/251 (deposit) / 229/249 (withdraw). Both sides are whole-replace (same
-- posture as usp_CharacterItems_ReplaceContainer/usp_AccountVault_SetItems), not per-slot patches -- the
-- caller has already computed each side's full final contents via the pure domain policy. Auto-creates the
-- AccountVault row on first use (same posture as usp_Gift_ClaimIntoVault) so a deposit never requires the
-- vault panel to have been opened first.
-- Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:2971-3182 (ProcessForInventoryToSave/ProcessForSaveToInventory).
CREATE PROCEDURE game.usp_AccountVault_TransferItemWithCharacter @CharacterId INT,
                                                                 @Container TINYINT,
                                                                 @Items game.tvp_CharacterItemSlot READONLY,
                                                                 @AccountId INT,
                                                                 @VaultItems game.tvp_AccountVaultItemSlot READONLY
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    BEGIN
        TRANSACTION;

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

    IF
        NOT EXISTS (SELECT 1 FROM game.AccountVault WHERE AccountId = @AccountId)
        INSERT INTO game.AccountVault (AccountId) VALUES (@AccountId);

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
