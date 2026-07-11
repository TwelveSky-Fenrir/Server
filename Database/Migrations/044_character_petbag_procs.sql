-- Migrations/044_character_petbag_procs.sql
-- 3 atomic procedures backing PetBagRepository (Infrastructure/Fenrir.Data/Inventory/PetBagRepository.cs), the
-- new IPetBagRepository this pass adds for the CZ_PROCESS_DATA_SEND pet-bag family (tSort 254/255/256). Must
-- be a migration (not StoredProcedures/), applied AFTER Migrations/043 creates game.CharacterPetBag --
-- StoredProcedures/ scripts run before migrations, and every one of these 3 procedures references that table.
-- Application-side (PetBagItemTransferPolicy) has already fully validated the request before any of these run
-- (pet-equipped/entitlement/catalog-sort/occupancy checks); the re-verification here (occupied-destination,
-- empty-source) closes the same TOCTOU window usp_Character_AdjustStoreMoney's own guarded UPDATE closes for
-- its family, not a duplicate of app-side validation.
CREATE OR ALTER PROCEDURE game.usp_CharacterPetBag_Deposit @CharacterId        INT,
                                                            @InventoryContainer TINYINT,
                                                            @InventoryItems     game.tvp_CharacterItemSlot READONLY,
                                                            @PetBagSlot         TINYINT,
                                                            @PetItemId          INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    IF EXISTS (SELECT 1 FROM game.CharacterPetBag WHERE CharacterId = @CharacterId AND Slot = @PetBagSlot)
        THROW 50350, N'Pet-bag destination slot already occupied.', 1;

    DELETE FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @InventoryContainer;

    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine, Refine,
                                     Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @CharacterId,
           @InventoryContainer,
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
    FROM @InventoryItems;

    INSERT INTO game.CharacterPetBag (CharacterId, Slot, ItemId)
    VALUES (@CharacterId, @PetBagSlot, @PetItemId);

    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE game.usp_CharacterPetBag_Withdraw @CharacterId        INT,
                                                             @PetBagSlot         TINYINT,
                                                             @InventoryContainer TINYINT,
                                                             @InventoryItems     game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM game.CharacterPetBag WHERE CharacterId = @CharacterId AND Slot = @PetBagSlot)
        THROW 50351, N'Pet-bag source slot is empty.', 1;

    DELETE FROM game.CharacterPetBag
    WHERE CharacterId = @CharacterId
      AND Slot = @PetBagSlot;

    DELETE FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @InventoryContainer;

    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine, Refine,
                                     Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @CharacterId,
           @InventoryContainer,
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
    FROM @InventoryItems;

    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE game.usp_CharacterPetBag_Rearrange @CharacterId    INT,
                                                              @SourceSlot      TINYINT,
                                                              @DestinationSlot TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DECLARE @ItemId INT;
    SELECT @ItemId = ItemId
    FROM game.CharacterPetBag
    WHERE CharacterId = @CharacterId
      AND Slot = @SourceSlot;

    IF @ItemId IS NULL
        THROW 50351, N'Pet-bag source slot is empty.', 1;

    IF EXISTS (SELECT 1 FROM game.CharacterPetBag WHERE CharacterId = @CharacterId AND Slot = @DestinationSlot)
        THROW 50350, N'Pet-bag destination slot already occupied.', 1;

    DELETE FROM game.CharacterPetBag
    WHERE CharacterId = @CharacterId
      AND Slot = @SourceSlot;

    INSERT INTO game.CharacterPetBag (CharacterId, Slot, ItemId)
    VALUES (@CharacterId, @DestinationSlot, @ItemId);

    COMMIT TRANSACTION;
END;
GO
