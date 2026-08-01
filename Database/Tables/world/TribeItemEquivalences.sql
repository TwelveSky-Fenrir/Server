-- Legacy tsItem[][3] (Server/ts25zone/S04_MyWork03.cpp:326-462 -- the __GOD__ build's live rows only; the
-- preceding #ifndef __GOD__ block at :241-323 is dead code in every real build and deliberately NOT seeded
-- here, see game.usp_Character_ApplyTribeConversion's own header for the full citation), the tribe-
-- conversion equipped-gear-equivalence table consumed by TChangeEquip (:509-585) when a character converts
-- to a different base tribe via the Book of Noble Dragon/Royal Serpent/Grand Tiger V2 (world.Items
-- 99014/99015/99016). GroupIndex is a Fenrir-only synthetic row id: the legacy table has no id column of
-- its own, just fixed array position. TribeId 0/1/2 mirrors the legacy array's own 3 columns (0=Noble
-- Dragon, 1=Royal Serpent, 2=Grand Tiger). A handful of rows carry the identical ItemId in all 3 tribe
-- columns (tribe-neutral gear needing no substitution) -- those are seeded verbatim, not specially marked.
-- The reserved id band 87129-87257 ("V2" alternates, offset +129 from this table's own 87000-87128 rows) is
-- NOT a separate set of rows here -- it is pure +129 arithmetic applied by the consuming procedure, per the
-- legacy source's own bSetV2 branch.
CREATE TABLE world.TribeItemEquivalences
(
    GroupIndex TINYINT NOT NULL,
    TribeId    TINYINT NOT NULL,
    ItemId     INT     NOT NULL,
    CONSTRAINT PK_TribeItemEquivalences PRIMARY KEY CLUSTERED (GroupIndex, TribeId),
    CONSTRAINT CK_TribeItemEquivalences_TribeId CHECK (TribeId BETWEEN 0 AND 2),
    CONSTRAINT UQ_TribeItemEquivalences_Tribe_Item UNIQUE (TribeId, ItemId),
    CONSTRAINT FK_TribeItemEquivalences_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
);
