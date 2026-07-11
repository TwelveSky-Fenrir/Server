-- Migrations/043_character_petbag_table.sql
-- New table backing the CZ_PROCESS_DATA_SEND pet-bag family (tSort 254 deposit/255 withdraw/256 rearrange) --
-- legacy wAvatar's 20-slot pet/animal cargo bag (aPetBagDate-adjacent array, Server/Header/Protocol/
-- STRUCT.h:520-521): a bare catalog-id-per-slot store with no quantity/value/serial/socket/expiration fields
-- at all, unlike every general-inventory/Store/Save slot -- see
-- Application/Fenrir.Application.Game.Domain/Inventory/PetBagItemTransferPolicy.cs's own remarks for the
-- full legacy citation trail this table backs. Row absence = empty slot, same convention as
-- game.CharacterHotkeys.
--
-- Created as a migration (not a Tables/ script), same precedent as Migrations/041/042's own header: this
-- table did not exist in the initial schema and every StoredProcedures/ script runs BEFORE migrations, so any
-- procedure referencing it must also be a migration (see Migrations/044_character_petbag_procs.sql).
CREATE TABLE game.CharacterPetBag
(
    CharacterId INT     NOT NULL,
    Slot        TINYINT NOT NULL,
    ItemId      INT     NOT NULL,
    CONSTRAINT PK_CharacterPetBag PRIMARY KEY CLUSTERED (CharacterId, Slot),
    CONSTRAINT CK_CharacterPetBag_Slot CHECK (Slot <= 19), -- MAX_PET_BAG_SLOT_NUM=20, DEFINE.h:393
    CONSTRAINT CK_CharacterPetBag_ItemId CHECK (ItemId >= 1),
    CONSTRAINT FK_CharacterPetBag_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId)
);
