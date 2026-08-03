-- 4th column renamed ItemDate -> EnchantValue to match game.CharacterCostumeSlots (the table 047/048's
-- usp_Character_PersistProgressBatch/PersistFinalFlush actually target) and the legacy field it carries:
-- the equipped item's packed enchant/combine/refine/socket value copied onto the costume slot at equip time
-- (Server/Header/Protocol/STRUCT.h aCostumeDate[], persisted under key EFIX_COSTUME_VALUE -- not a date).
-- CaeriusNet TVPs bind by ordinal position, not name, so this rename requires no C# change to compile or
-- run correctly; CharacterCostumeSlotTvp.cs's 4th parameter is still named ItemDate as a cosmetic-only
-- mismatch left for fenrir-gameplay-domain-engineer to rename at their discretion.
CREATE TYPE game.tvp_CharacterCostumeSlot AS TABLE
(
    CharacterId  INT     NOT NULL,
    Slot         TINYINT NOT NULL,
    ItemId       INT     NOT NULL,
    EnchantValue INT     NOT NULL,
    ExpireDate   INT     NOT NULL
);
