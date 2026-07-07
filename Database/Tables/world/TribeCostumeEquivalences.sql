-- Legacy tsFation[][3] (Server/ts25zone/S04_MyWork03.cpp:465-506), the tribe-conversion worn-costume-
-- equivalence table consumed by TChangeEquip's costume sub-block (:570-583) when a character converts to a
-- different base tribe via the Book of Noble Dragon/Royal Serpent/Grand Tiger V2 (world.Items
-- 99014/99015/99016). GroupIndex is a Fenrir-only synthetic row id: the legacy table has no id column of
-- its own, just fixed array position. TribeId 0/1/2 mirrors the legacy array's own 3 columns (0=Noble
-- Dragon, 1=Royal Serpent, 2=Grand Tiger). A number of legacy rows are commented out in the source itself
-- (region-specific seasonal costumes never enabled for this build) -- only the live, uncommented rows are
-- seeded here, matching the count actually compiled.
-- IMPORTANT: Fenrir has no durable per-character costume-wardrobe storage yet (see
-- PlayerRuntimeState.CostumeWardrobe's own "no acquisition path yet" remarks) -- this table only supplies
-- the lookup data; the actual in-memory wardrobe remap on a tribe conversion is necessarily an
-- Application-layer follow-up, not something game.usp_Character_ApplyTribeConversion can persist today.
CREATE TABLE world.TribeCostumeEquivalences
(
    GroupIndex TINYINT NOT NULL,
    TribeId    TINYINT NOT NULL,
    ItemId     INT     NOT NULL,
    CONSTRAINT PK_TribeCostumeEquivalences PRIMARY KEY CLUSTERED (GroupIndex, TribeId),
    CONSTRAINT CK_TribeCostumeEquivalences_TribeId CHECK (TribeId BETWEEN 0 AND 2),
    CONSTRAINT UQ_TribeCostumeEquivalences_Tribe_Item UNIQUE (TribeId, ItemId),
    CONSTRAINT FK_TribeCostumeEquivalences_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
);
